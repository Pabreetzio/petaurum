using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Petaurum.Windows.Discovery;

/// <summary>
/// Collects names over mDNS / DNS-SD. Queries are one-shot (RFC 6762 §5.1): they go out from an
/// ordinary UDP port, so responders reply by unicast and nothing needs to bind port 5353 (which
/// Windows' own mDNS responder holds). See specs/features/device-list.md, "Resolve names".
/// </summary>
/// <remarks>
/// Start it at the beginning of a scan so the service browse overlaps the ping sweep, then call
/// <see cref="QueryAddresses"/> once the device IPs are known, and <see cref="GetNames"/> at the deadline.
/// </remarks>
public sealed partial class MdnsNames : IAsyncDisposable
{
    private const int Port = 5353;
    private static readonly IPEndPoint Group = new(IPAddress.Parse("224.0.0.251"), Port);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromMilliseconds(500);
    private const int MaxQuestionsPerPacket = 32;
    private const string ServicesQuery = "_services._dns-sd._udp.local";

    // Most preferred first when a device advertises several; types found by the _services browse come after.
    private static readonly string[] ServiceTypes =
    [
        "_googlecast._tcp", "_airplay._tcp", "_raop._tcp", "_companion-link._tcp", "_device-info._tcp",
        "_spotify-connect._tcp", "_sonos._tcp", "_hap._tcp", "_homekit._tcp", "_hue._tcp",
        "_ipp._tcp", "_ipps._tcp", "_printer._tcp", "_pdl-datastream._tcp", "_scanner._tcp", "_uscan._tcp",
        "_http._tcp", "_smb._tcp", "_afpovertcp._tcp", "_workstation._tcp", "_sftp-ssh._tcp", "_ssh._tcp",
    ];

    private sealed class Instance(string label, string serviceType, IPAddress sender)
    {
        public string Label { get; } = label;
        public string ServiceType { get; } = serviceType;
        public IPAddress Sender { get; } = sender;
        public string? TargetHost { get; set; }
        public string? FriendlyName { get; set; }
    }

    private readonly List<(LocalSubnet Subnet, Socket Socket)> _sockets;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _tasks = [];
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<IPAddress>> _hostAddresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IPAddress, string> _reverseNames = new();
    private readonly Dictionary<string, Instance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _browsedTypes = new(ServiceTypes, StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _foundTypes = [];
    private ushort _nextId = (ushort)Random.Shared.Next(1, ushort.MaxValue);

    private MdnsNames(List<(LocalSubnet, Socket)> sockets) => _sockets = sockets;

    /// <summary>Opens a socket on each subnet and starts browsing for services.</summary>
    public static MdnsNames Start(IEnumerable<LocalSubnet> subnets)
    {
        var sockets = new List<(LocalSubnet, Socket)>();
        foreach (var subnet in subnets)
        {
            try
            {
                var socket = Udp.Open(subnet.LocalAddress);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, subnet.LocalAddress.GetAddressBytes());
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
                sockets.Add((subnet, socket));
            }
            catch (SocketException)
            {
                // That interface can't do multicast; the others may still work.
            }
        }

        var mdns = new MdnsNames(sockets);
        foreach (var (_, socket) in sockets)
            mdns._tasks.Add(Udp.ReceiveAsync(socket, mdns.Handle, mdns._stop.Token));

        var browse = Browse([ServicesQuery, .. ServiceTypes]);
        mdns.SendTwice(sockets.Select(s => (s.Item2, mdns.NewQuery(browse), Group)).ToList());
        return mdns;
    }

    /// <summary>
    /// Asks each address for its reverse PTR and its services, both directly and by multicast,
    /// including any service types the <c>_services</c> browse turned up.
    /// </summary>
    public void QueryAddresses(IReadOnlyCollection<IPAddress> addresses)
    {
        List<string> newTypes;
        lock (_lock)
        {
            newTypes = _foundTypes.Where(_browsedTypes.Add).ToList();
        }
        var browse = Browse([ServicesQuery, .. ServiceTypes, .. newTypes]);

        var packets = new List<(Socket, byte[], IPEndPoint)>();
        foreach (var (subnet, socket) in _sockets)
        {
            var local = addresses.Where(subnet.Contains).ToList();
            // Some devices (Google Cast) ignore one-shot queries to the group but answer ones sent straight to them.
            foreach (var address in local)
                foreach (var chunk in browse.Prepend((ReverseName(address), DnsMessage.TypePtr)).Chunk(MaxQuestionsPerPacket))
                    packets.Add((socket, NewQuery(chunk), new IPEndPoint(address, Port)));
            foreach (var chunk in local.Select(a => (ReverseName(a), DnsMessage.TypePtr)).Chunk(MaxQuestionsPerPacket))
                packets.Add((socket, NewQuery(chunk), Group));
            foreach (var chunk in Browse(newTypes).Chunk(MaxQuestionsPerPacket))
                packets.Add((socket, NewQuery(chunk), Group));
        }

        SendTwice(packets);
    }

    /// <summary>What mDNS has said so far about each address: its best service name and its host name.</summary>
    public Dictionary<IPAddress, (string? ServiceName, string? HostName)> GetNames(IEnumerable<IPAddress> addresses)
    {
        lock (_lock)
        {
            var names = new Dictionary<IPAddress, (string?, string?)>();
            foreach (var address in addresses)
            {
                var serviceName = _instances.Values
                    .Where(i => BelongsTo(i, address))
                    .OrderBy(i => ServiceRank(i.ServiceType))
                    .Select(PreferredName)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && !LooksMachineGenerated(n));

                var hostName = _reverseNames.GetValueOrDefault(address)
                    ?? _hostAddresses.Where(h => h.Value.Contains(address)).Select(h => h.Key).FirstOrDefault();

                if (serviceName is not null || hostName is not null)
                    names[address] = (serviceName, hostName);
            }
            return names;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await Task.WhenAll(_tasks);
        foreach (var (_, socket) in _sockets) socket.Dispose();
        _stop.Dispose();
    }

    private void Handle(IPAddress sender, byte[] reply)
    {
        var records = DnsMessage.ParseRecords(reply);
        lock (_lock)
        {
            foreach (var record in records)
            {
                switch (record)
                {
                    case DnsMessage.A a:
                        var host = string.Join('.', a.Name);
                        if (!_hostAddresses.TryGetValue(host, out var addresses))
                            _hostAddresses[host] = addresses = [];
                        addresses.Add(a.Address);
                        break;
                    case DnsMessage.Ptr ptr when ParseReverseName(ptr.Name) is { } address:
                        _reverseNames.TryAdd(address, string.Join('.', ptr.Target));
                        break;
                    case DnsMessage.Ptr ptr when string.Join('.', ptr.Name).Equals(ServicesQuery, StringComparison.OrdinalIgnoreCase):
                        if (ptr.Target is [.. var type, "local"] && !_foundTypes.Contains(string.Join('.', type), StringComparer.OrdinalIgnoreCase))
                            _foundTypes.Add(string.Join('.', type));
                        break;
                    case DnsMessage.Ptr ptr:
                        FindInstance(ptr.Target, sender);
                        break;
                    case DnsMessage.Srv srv when FindInstance(srv.Name, sender) is { } instance:
                        instance.TargetHost = string.Join('.', srv.Target);
                        break;
                    case DnsMessage.Txt txt when txt.Values.TryGetValue("fn", out var friendlyName)
                                                 && FindInstance(txt.Name, sender) is { } instance:
                        instance.FriendlyName = friendlyName;
                        break;
                }
            }
        }
    }

    /// <summary>The instance a service name like "Kitchen._airplay._tcp.local" refers to, recorded on first sight.</summary>
    private Instance? FindInstance(string[] name, IPAddress sender)
    {
        if (name is not [var label, var service, var protocol and ("_tcp" or "_udp"), "local"]) return null;
        var key = string.Join('.', name);
        if (!_instances.TryGetValue(key, out var instance))
            _instances[key] = instance = new Instance(label, $"{service}.{protocol}", sender);
        return instance;
    }

    /// <summary>An instance belongs to the address its SRV target resolves to, or failing that, to whoever announced it.</summary>
    private bool BelongsTo(Instance instance, IPAddress address) =>
        instance.TargetHost is not null && _hostAddresses.TryGetValue(instance.TargetHost, out var addresses)
            ? addresses.Contains(address)
            : instance.Sender.Equals(address);

    private static string? PreferredName(Instance instance) => instance.ServiceType switch
    {
        // Cast instance names are "<Model>-<uuid>"; the name the user chose is in TXT "fn". A speaker
        // also advertises the groups it leads ("Home group"), which aren't its own name.
        "_googlecast._tcp" => instance.Label.StartsWith("Google-Cast-Group-", StringComparison.OrdinalIgnoreCase)
            ? null
            : instance.FriendlyName,
        // "<MAC>@Living Room"
        "_raop._tcp" => instance.Label[(instance.Label.IndexOf('@') + 1)..],
        // "my-pc [aa:bb:cc:dd:ee:ff]"
        "_workstation._tcp" => WorkstationSuffix().Replace(instance.Label, ""),
        _ => instance.Label,
    };

    private int ServiceRank(string serviceType)
    {
        var rank = Array.FindIndex(ServiceTypes, t => t.Equals(serviceType, StringComparison.OrdinalIgnoreCase));
        return rank >= 0 ? rank : ServiceTypes.Length;
    }

    private static bool LooksMachineGenerated(string name) => LongHexRun().IsMatch(name.Replace("-", ""));

    private static List<(string, ushort)> Browse(IEnumerable<string> names) =>
        names.Select(n => (n.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ? n : n + ".local", DnsMessage.TypePtr)).ToList();

    private static string ReverseName(IPAddress address) =>
        string.Join('.', address.GetAddressBytes().Reverse()) + ".in-addr.arpa";

    private static IPAddress? ParseReverseName(string[] name) =>
        name is [var d, var c, var b, var a, var inAddr, var arpa]
        && inAddr.Equals("in-addr", StringComparison.OrdinalIgnoreCase)
        && arpa.Equals("arpa", StringComparison.OrdinalIgnoreCase)
        && IPAddress.TryParse($"{a}.{b}.{c}.{d}", out var address)
            ? address
            : null;

    private byte[] NewQuery(IReadOnlyCollection<(string, ushort)> questions)
    {
        // Legacy one-shot replies echo the ID; keep it non-zero so they're easy to tell from announcements.
        var id = _nextId++;
        if (id == 0) id = _nextId++;
        return DnsMessage.Query(id, questions);
    }

    /// <summary>Sends every packet, waits a moment, and sends them again in case UDP dropped some.</summary>
    private void SendTwice(IReadOnlyList<(Socket Socket, byte[] Datagram, IPEndPoint Target)> packets)
    {
        if (packets.Count == 0) return;
        var token = _stop.Token;
        _tasks.Add(Task.Run(async () =>
        {
            try
            {
                for (var round = 0; round < 2; round++)
                {
                    foreach (var (socket, datagram, target) in packets)
                        await Udp.SendAsync(socket, datagram, target, token);
                    await Task.Delay(ResendDelay, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }));
    }

    [GeneratedRegex(@"\s*\[[0-9a-fA-F:]+\]$")]
    private static partial Regex WorkstationSuffix();

    [GeneratedRegex("[0-9a-fA-F]{12,}")]
    private static partial Regex LongHexRun();
}
