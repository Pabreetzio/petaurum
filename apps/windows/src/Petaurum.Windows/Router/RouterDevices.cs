using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Petaurum.Windows.Discovery;

namespace Petaurum.Windows.Router;

/// <summary>What the router knows about one device. See specs/features/router.md, "Device list".</summary>
/// <param name="Mac">Upper-case and colon separated, like DeviceItem.MacText.</param>
/// <param name="IsOn">The router's Status is "on" (and the page is fresh enough to trust it).</param>
public sealed record RouterDeviceInfo(
    string Mac, string? Ip, string? Name, bool IsOn, DateTimeOffset? LastActivity, string? Connection, int? SignalDbm);

/// <summary>Pulls per-device info out of the stored Device List and Home Network pages.</summary>
public static partial class RouterDevices
{
    /// <summary>The router reports status at the time it was read; after this long, "on" no longer means online.</summary>
    private static readonly TimeSpan StatusMaxAge = TimeSpan.FromMinutes(10);

    public static IReadOnlyDictionary<string, RouterDeviceInfo> From(
        RouterPageData? deviceList, DateTimeOffset? deviceListReadAt, RouterPageData? homeNetwork)
    {
        var result = new Dictionary<string, RouterDeviceInfo>();
        if (deviceList is null) return result;
        var statusIsFresh = deviceListReadAt is { } readAt && DateTimeOffset.Now - readAt < StatusMaxAge;
        var signals = Signals(homeNetwork);

        var records = deviceList.Sections.SelectMany(s => s.Blocks).SelectMany(b => b.Records ?? []);
        foreach (var record in records)
        {
            // Devices with IPv6 addresses repeat some labels; the first of each is the one that matters.
            var fields = record.Fields.GroupBy(f => f.Label).ToDictionary(g => g.Key, g => g.First().Value);
            if (NormalizeMac(fields.GetValueOrDefault("MAC Address")) is not { } mac) continue;

            string? ip = null, name = fields.GetValueOrDefault("Name");
            if (fields.GetValueOrDefault("IPv4 Address / Name") is { } ipAndName)
            {
                var parts = ipAndName.Split(" / ", 2, StringSplitOptions.TrimEntries);
                if (IPAddress.TryParse(parts[0], out _)) ip = parts[0];
                if (parts.Length > 1) name ??= parts[1];
            }
            if (name is not null && (name == ip || MachineNames.LooksMachineGenerated(name))) name = null;

            result[mac] = new RouterDeviceInfo(mac, ip, name,
                statusIsFresh && fields.GetValueOrDefault("Status") == "on",
                ParseActivity(fields.GetValueOrDefault("Last Activity")),
                Connection(fields.GetValueOrDefault("Connection Type")),
                signals.TryGetValue(mac, out var dbm) ? dbm : null);
        }
        return result;
    }

    /// <summary>Signal strength by MAC, from Home Network's Wi-Fi Client Connection Statistics table.</summary>
    private static Dictionary<string, int> Signals(RouterPageData? homeNetwork)
    {
        var signals = new Dictionary<string, int>();
        var tables = homeNetwork?.Sections.SelectMany(s => s.Blocks).Where(b => b.Kind == RouterBlockKind.Table) ?? [];
        foreach (var table in tables)
        {
            var macColumn = table.Columns!.IndexOf("MAC Address");
            var signalColumn = table.Columns.IndexOf("Signal Strength");
            if (macColumn < 0 || signalColumn < 0) continue;
            foreach (var row in table.Rows!)
            {
                if (row.Count <= Math.Max(macColumn, signalColumn)) continue;
                var match = Dbm().Match(row[signalColumn]);
                if (NormalizeMac(row[macColumn]) is { } mac && match.Success)
                    signals[mac] = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }
        return signals;
    }

    /// <summary>"Ethernet LAN-3" gives "Ethernet port 3"; Wi-Fi gives "Wi-Fi" and the band, such as "Wi-Fi 5 GHz".</summary>
    private static string? Connection(string? connectionType)
    {
        if (string.IsNullOrWhiteSpace(connectionType)) return null;
        if (EthernetPort().Match(connectionType) is { Success: true } port) return $"Ethernet port {port.Groups[1].Value}";
        if (connectionType.StartsWith("Wi-Fi", StringComparison.OrdinalIgnoreCase))
            return WifiBand().Match(connectionType) is { Success: true } band ? $"Wi-Fi {band.Groups[1].Value} GHz" : "Wi-Fi";
        return connectionType.Split('\n')[0];
    }

    /// <summary>The router's local time, such as "Wed Oct  7 17:41:11 2026" (single-digit days are space padded).</summary>
    private static DateTimeOffset? ParseActivity(string? text)
    {
        if (text is null) return null;
        var collapsed = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return DateTime.TryParseExact(collapsed, "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var time)
            ? new DateTimeOffset(time)
            : null;
    }

    /// <summary>"00:05:cd:f2:c6:80" as "00:05:CD:F2:C6:80", or null if it isn't a MAC.</summary>
    private static string? NormalizeMac(string? text)
    {
        if (text is null) return null;
        var parts = text.Trim().Split(':');
        return parts.Length == 6 && parts.All(p => p.Length == 2 && p.All(char.IsAsciiHexDigit))
            ? string.Join(":", parts).ToUpperInvariant()
            : null;
    }

    [GeneratedRegex(@"\bLAN-(\d+)")]
    private static partial Regex EthernetPort();

    [GeneratedRegex(@"(\d+(?:\.\d+)?) GHz")]
    private static partial Regex WifiBand();

    [GeneratedRegex(@"(-?\d+) dBm")]
    private static partial Regex Dbm();
}
