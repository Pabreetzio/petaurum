using System.IO;
using System.Net.NetworkInformation;

namespace Petaurum.Windows.Discovery;

/// <summary>
/// Looks up who made a network interface from its MAC prefix, using the IEEE registry bundled as
/// oui.txt (regenerate it with tools/Update-OuiList.ps1). See specs/domain/device.md, "Vendor".
/// </summary>
public static class OuiVendors
{
    private const string ResourceName = "Petaurum.Windows.Discovery.oui.txt";

    // MA-S, MA-M and MA-L prefixes, in hex digits: longest match wins.
    private static readonly int[] PrefixLengths = [9, 7, 6];

    private static readonly Lazy<Dictionary<string, string>> Vendors = new(Load);

    public static string? Find(PhysicalAddress? mac)
    {
        if (mac is null || IsLocallyAdministered(mac)) return null;
        var hex = mac.ToString();
        if (hex.Length != 12) return null;
        foreach (var length in PrefixLengths)
            if (Vendors.Value.TryGetValue(hex[..length], out var vendor))
                return vendor;
        return null;
    }

    /// <summary>True for MACs a device made up itself, such as a phone's private Wi-Fi address.</summary>
    public static bool IsLocallyAdministered(PhysicalAddress mac) =>
        mac.GetAddressBytes() is [var first, ..] && (first & 0x02) != 0;

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(OuiVendors).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        var vendors = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (tab > 0) vendors[line[..tab]] = line[(tab + 1)..];
        }
        return vendors;
    }
}
