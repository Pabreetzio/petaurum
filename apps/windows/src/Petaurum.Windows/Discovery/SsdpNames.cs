using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Petaurum.Windows.Discovery;

/// <summary>
/// Collects names over SSDP / UPnP: an M-SEARCH turns up each device's description URL, and the
/// description carries its friendly name and model. Searches go out from an ordinary UDP port, so
/// replies come back by unicast and nothing needs to bind port 1900 (which Windows' SSDP service
/// holds). See specs/features/device-list.md, "Resolve names".
/// </summary>
/// <remarks>
/// Start it at the beginning of a scan so the multicast search overlaps the ping sweep, then call
/// <see cref="QueryAddresses"/> once the device IPs are known, and <see cref="GetNames"/> at the deadline.
/// </remarks>
public sealed partial class SsdpNames : IAsyncDisposable
{
    private const int Port = 1900;
    private static readonly IPEndPoint Group = new(IPAddress.Parse("239.255.255.250"), Port);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DescriptionTimeout = TimeSpan.FromMilliseconds(1500);
    private const int MaxDescriptionsPerDevice = 4;

    private static readonly byte[] Search = Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "MAN: \"ssdp:discover\"\r\n" +
        "MX: 1\r\n" +
        "ST: ssdp:all\r\n" +
        "\r\n");

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        // Device descriptions live on the LAN; a system proxy would only get in the way.
        UseProxy = false,
    })
    {
        MaxResponseContentBufferSize = 256 * 1024,
    };

    private sealed record Description(string? FriendlyName, string? Manufacturer, string? ModelName, string? DeviceType);

    private readonly List<(LocalSubnet Subnet, Socket Socket)> _sockets;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _tasks = [];
    private readonly object _lock = new();
    // Description URLs in the order they were first seen, and what each one said once fetched.
    private readonly Dictionary<IPAddress, List<Uri>> _locations = new();
    private readonly Dictionary<Uri, Description> _descriptions = new();

    private SsdpNames(List<(LocalSubnet, Socket)> sockets) => _sockets = sockets;

    /// <summary>Opens a socket on each subnet and searches the multicast group.</summary>
    public static SsdpNames Start(IEnumerable<LocalSubnet> subnets)
    {
        var sockets = new List<(LocalSubnet, Socket)>();
        foreach (var subnet in subnets)
        {
            try
            {
                var socket = Udp.Open(subnet.LocalAddress);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, subnet.LocalAddress.GetAddressBytes());
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                sockets.Add((subnet, socket));
            }
            catch (SocketException)
            {
                // That interface can't do multicast; the others may still work.
            }
        }

        var ssdp = new SsdpNames(sockets);
        lock (ssdp._lock)
            foreach (var (_, socket) in sockets)
                ssdp._tasks.Add(Udp.ReceiveAsync(socket, ssdp.Handle, ssdp._stop.Token));

        ssdp.SendTwice(sockets.Select(s => (s.Item2, Group)).ToList());
        return ssdp;
    }

    /// <summary>
    /// Searches each address directly, for devices that ignore searches sent to the group, and the
    /// group again, for devices whose replies were lost while the ping sweep kept them busy.
    /// </summary>
    public void QueryAddresses(IReadOnlyCollection<IPAddress> addresses) =>
        SendTwice(_sockets
            .SelectMany(s => addresses.Where(s.Subnet.Contains).Select(a => (s.Socket, new IPEndPoint(a, Port))).Append((s.Socket, Group)))
            .ToList());

    /// <summary>What UPnP has said so far about each address: its friendly name, and a model to fall back on.</summary>
    public Dictionary<IPAddress, (string? FriendlyName, string? Model)> GetNames(IEnumerable<IPAddress> addresses)
    {
        lock (_lock)
        {
            var names = new Dictionary<IPAddress, (string?, string?)>();
            foreach (var address in addresses)
            {
                if (!_locations.TryGetValue(address, out var locations)) continue;
                var descriptions = locations.Select(l => _descriptions.GetValueOrDefault(l)).OfType<Description>().ToList();

                var friendlyName = descriptions
                    .Select(d => CleanFriendlyName(d.FriendlyName))
                    .FirstOrDefault(n => !string.IsNullOrEmpty(n) && !MachineNames.LooksMachineGenerated(n));
                var model = descriptions.Select(Model).FirstOrDefault(n => !string.IsNullOrEmpty(n));

                if (friendlyName is not null || model is not null)
                    names[address] = (friendlyName, model);
            }
            return names;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        Task[] tasks;
        lock (_lock) tasks = [.. _tasks];
        await Task.WhenAll(tasks);
        foreach (var (_, socket) in _sockets) socket.Dispose();
        _stop.Dispose();
    }

    private void Handle(IPAddress sender, byte[] reply)
    {
        // An HTTP/1.1 200 OK whose LOCATION header points at the device description.
        var lines = Encoding.ASCII.GetString(reply).Split("\r\n");
        if (!lines[0].StartsWith("HTTP/1.", StringComparison.OrdinalIgnoreCase) || !lines[0].Contains(" 200")) return;
        var location = lines
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2 && p[0].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[1].Trim())
            .FirstOrDefault();

        // Only fetch from the device that replied, so one reply can't send us anywhere else.
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp
            || !IPAddress.TryParse(uri.Host, out var host)
            || !host.Equals(sender))
            return;

        lock (_lock)
        {
            if (!_locations.TryGetValue(sender, out var locations))
                _locations[sender] = locations = [];
            if (locations.Contains(uri) || locations.Count >= MaxDescriptionsPerDevice) return;
            locations.Add(uri);
            _tasks.Add(FetchAsync(uri));
        }
    }

    private async Task FetchAsync(Uri uri)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(DescriptionTimeout);
            var xml = await Http.GetByteArrayAsync(uri, timeout.Token);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(xml), settings);
            var document = XDocument.Load(reader);

            // Namespaces vary between vendors (and some get them wrong), so match on local names only.
            var device = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "device");
            if (device is null) return;
            string? Field(string name) => device.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

            var description = new Description(Field("friendlyName"), Field("manufacturer"), Field("modelName"), Field("deviceType"));
            lock (_lock) _descriptions[uri] = description;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or XmlException
                                      or IOException or ObjectDisposedException)
        {
            // No description, or not in time; the device just goes without a UPnP name.
        }
    }

    /// <summary>Drops an IP address the device put in its own name, e.g. "C4-VDB-W(192.168.1.239)".</summary>
    private static string? CleanFriendlyName(string? name) =>
        name is null ? null : IpAddressInName().Replace(name, "").Trim(' ', '-', ':', '_');

    /// <summary>"Manufacturer ModelName", or failing that, the vendor domain of a non-standard device type.</summary>
    private static string? Model(Description description)
    {
        var manufacturer = description.Manufacturer ?? "";
        var model = description.ModelName ?? "";
        var text = model.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase) ? model
            : manufacturer.StartsWith(model, StringComparison.OrdinalIgnoreCase) ? manufacturer
            : $"{manufacturer} {model}";
        if (!string.IsNullOrWhiteSpace(text)) return text.Trim();

        // "urn:AN-210-SW-8-POE:device:Switch:1". Standard and vendor schema domains look like
        // "schemas-upnp-org" or "dial-multiscreen-org" and say nothing about the model.
        return description.DeviceType?.Split(':') is ["urn", var domain, "device", _, _] && !SchemaDomain().IsMatch(domain)
            ? domain
            : null;
    }

    /// <summary>Sends every search, waits a moment, and sends them again in case UDP dropped some.</summary>
    private void SendTwice(IReadOnlyList<(Socket Socket, IPEndPoint Target)> searches)
    {
        if (searches.Count == 0) return;
        var token = _stop.Token;
        var sending = Task.Run(async () =>
        {
            try
            {
                for (var round = 0; round < 2; round++)
                {
                    foreach (var (socket, target) in searches)
                        await Udp.SendAsync(socket, Search, target, token);
                    await Task.Delay(ResendDelay, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
        lock (_lock) _tasks.Add(sending);
    }

    [GeneratedRegex(@"\s*[(\[]?\b\d{1,3}(\.\d{1,3}){3}\b[)\]]?")]
    private static partial Regex IpAddressInName();

    [GeneratedRegex(@"^[a-z0-9.-]+-[a-z]{2,}$")]
    private static partial Regex SchemaDomain();

}
