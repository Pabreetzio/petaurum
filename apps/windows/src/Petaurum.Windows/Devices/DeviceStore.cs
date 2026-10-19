using Petaurum.Windows.Storage;

namespace Petaurum.Windows.Devices;

/// <summary>Remembered devices in devices.json. See specs/features/device-list.md, "Memory".</summary>
internal static class DeviceStore
{
    private const string FileName = "devices.json";

    public sealed record StoredDevice(
        string Ip, string? Mac, string? HostName, string? CustomName, string? Vendor,
        bool IsThisComputer, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);

    public sealed record Snapshot(DateTimeOffset? LastScan, List<StoredDevice> Devices);

    /// <summary>Loads remembered devices; they all start offline until a scan finds them.</summary>
    public static (DateTimeOffset? LastScan, List<DeviceItem> Devices) Load()
    {
        var snapshot = LocalStore.Read<Snapshot>(FileName);
        if (snapshot is null) return (null, []);
        var devices = snapshot.Devices.Select(d => new DeviceItem(d.Mac, d.Ip, d.FirstSeen)
        {
            HostName = d.HostName,
            CustomName = d.CustomName,
            VendorText = d.Vendor,
            IsThisComputer = d.IsThisComputer,
            LastSeen = d.LastSeen,
        }).ToList();
        return (snapshot.LastScan, devices);
    }

    public static void Save(DateTimeOffset? lastScan, IEnumerable<DeviceItem> devices) =>
        LocalStore.Write(FileName, new Snapshot(lastScan, devices
            .Select(d => new StoredDevice(d.IpText, d.MacText, d.HostName, d.CustomName, d.VendorText,
                d.IsThisComputer, d.FirstSeen, d.LastSeen))
            .ToList()));
}
