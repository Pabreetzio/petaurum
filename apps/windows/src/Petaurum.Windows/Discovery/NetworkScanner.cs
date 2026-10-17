using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Petaurum.Windows.Discovery;

/// <summary>Finds devices on the local network. See specs/features/device-list.md, "Discovery".</summary>
public sealed class NetworkScanner
{
    private const int PingTimeoutMs = 750;
    private const int MaxConcurrentPings = 128;
    private static readonly TimeSpan NameLookupTimeout = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<Device>> ScanAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var subnets = LocalSubnet.Discover();
        if (subnets.Count == 0)
            throw new InvalidOperationException("No active IPv4 network connection found.");

        var targets = subnets.SelectMany(s => s.Hosts()).Distinct().ToList();
        progress?.Report($"Pinging {targets.Count} addresses…");
        var responders = await PingSweepAsync(targets, cancellationToken);

        progress?.Report("Reading the ARP table…");
        var found = new Dictionary<IPAddress, (PhysicalAddress? Mac, bool IsSelf)>();
        foreach (var entry in ArpTable.Read())
            if (subnets.Any(s => s.Contains(entry.IpAddress)))
                found[entry.IpAddress] = (entry.MacAddress, false);
        foreach (var ip in responders)
            found.TryAdd(ip, (null, false));
        foreach (var subnet in subnets)
            found[subnet.LocalAddress] = (subnet.LocalMac, true);

        progress?.Report($"Resolving names for {found.Count} devices…");
        var names = await ResolveNamesAsync(found.Keys, cancellationToken);

        var now = DateTimeOffset.Now;
        return found
            .Select(kv =>
            {
                // DNS can name this computer something unhelpful (Docker maps it to host.docker.internal).
                var name = kv.Value.IsSelf ? Environment.MachineName : names.GetValueOrDefault(kv.Key);
                return new Device(kv.Key, kv.Value.Mac, name, kv.Value.IsSelf,
                    kv.Value.IsSelf || responders.Contains(kv.Key), now);
            })
            .OrderBy(d => d.SortKey)
            .ToList();
    }

    private static async Task<HashSet<IPAddress>> PingSweepAsync(IEnumerable<IPAddress> targets, CancellationToken cancellationToken)
    {
        var responders = new HashSet<IPAddress>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentPings, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(targets, options, async (ip, ct) =>
        {
            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(PingTimeoutMs), cancellationToken: ct);
                if (reply.Status == IPStatus.Success)
                    lock (responders) responders.Add(ip);
            }
            catch (PingException)
            {
                // Unreachable or no route; the ARP table may still have learned something.
            }
        });
        return responders;
    }

    private static async Task<Dictionary<IPAddress, string>> ResolveNamesAsync(IEnumerable<IPAddress> addresses, CancellationToken cancellationToken)
    {
        var lookups = addresses.Select(async ip =>
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip.ToString(), cancellationToken).WaitAsync(NameLookupTimeout, cancellationToken);
                // A failed reverse lookup sometimes echoes the address back as the name.
                return (Ip: ip, Name: entry.HostName == ip.ToString() ? null : entry.HostName);
            }
            catch (Exception e) when (e is SocketException or TimeoutException)
            {
                return (Ip: ip, Name: (string?)null);
            }
        });

        var results = await Task.WhenAll(lookups);
        return results
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToDictionary(r => r.Ip, r => r.Name!);
    }
}
