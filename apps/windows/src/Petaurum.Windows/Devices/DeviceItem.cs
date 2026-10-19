using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Petaurum.Windows.Discovery;
using Petaurum.Windows.Router;

namespace Petaurum.Windows.Devices;

/// <summary>A remembered device as shown in the list. See specs/domain/device.md.</summary>
public sealed class DeviceItem : INotifyPropertyChanged
{
    private string? _customName;
    private string? _hostName;
    private string _ipText = "";
    private bool? _isOnline;
    private DateTimeOffset _lastSeen;
    private string? _routerName;
    private string? _connection;
    private int? _signalDbm;

    public DeviceItem(string? macText, string ipText, DateTimeOffset firstSeen)
    {
        MacText = macText;
        _ipText = ipText;
        FirstSeen = firstSeen;
        _lastSeen = firstSeen;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The device's identity: its MAC when known, otherwise its IP. See "Identity".</summary>
    public string Key => KeyFor(MacText, IpText);

    public static string KeyFor(string? macText, string ipText) => macText is null ? "ip:" + ipText : "mac:" + macText;

    /// <summary>For x:Bind, which can't pass the item itself to an event handler.</summary>
    public DeviceItem Self => this;

    /// <summary>Upper-case and colon separated, or null when unknown. Fixed once known.</summary>
    public string? MacText { get; private set; }

    public string IpText
    {
        get => _ipText;
        set
        {
            if (Set(ref _ipText, value))
            {
                OnPropertyChanged(nameof(WebUri));
                OnPropertyChanged(nameof(HasAddress));
                OnPropertyChanged(nameof(SortKey));
            }
        }
    }

    public string? HostName
    {
        get => _hostName;
        set { if (Set(ref _hostName, value)) OnPropertyChanged(nameof(DisplayName)); }
    }

    public string? CustomName
    {
        get => _customName;
        set
        {
            if (Set(ref _customName, string.IsNullOrWhiteSpace(value) ? null : value.Trim()))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>The name on the router's device list, used when no scan has named the device.</summary>
    public string? RouterName
    {
        get => _routerName;
        set { if (Set(ref _routerName, value)) OnPropertyChanged(nameof(DisplayName)); }
    }

    /// <summary>How it reaches the router, such as "Ethernet port 3" or "Wi-Fi 5 GHz".</summary>
    public string? Connection
    {
        get => _connection;
        set { if (Set(ref _connection, value)) OnPropertyChanged(nameof(Details)); }
    }

    /// <summary>Wi-Fi signal strength at the router, while connected.</summary>
    public int? SignalDbm
    {
        get => _signalDbm;
        set { if (Set(ref _signalDbm, value)) OnPropertyChanged(nameof(Details)); }
    }

    public string? VendorText { get; set; }
    public bool IsThisComputer { get; set; }
    public DateTimeOffset FirstSeen { get; }

    public DateTimeOffset LastSeen
    {
        get => _lastSeen;
        set { if (Set(ref _lastSeen, value)) OnPropertyChanged(nameof(Details)); }
    }

    /// <summary>Whether the latest scan found it; null until this launch's first scan finishes.</summary>
    public bool? IsOnline
    {
        get => _isOnline;
        set { if (Set(ref _isOnline, value)) { OnPropertyChanged(nameof(Details)); OnPropertyChanged(nameof(IconOpacity)); } }
    }

    public string DisplayName =>
        (CustomName ?? HostName ?? RouterName ?? "Unknown device") + (IsThisComputer ? " (this computer)" : "");

    /// <summary>Vendor, connection and signal, and for a device not online now, when it was last seen.</summary>
    public string? Details
    {
        get
        {
            var connection = Connection is null ? null
                : SignalDbm is { } dbm && IsOnline != false ? $"{Connection} · {dbm} dBm" : Connection;
            var seen = IsOnline switch
            {
                false => $"Offline, last seen {Describe(LastSeen)}",
                null => $"Last seen {Describe(LastSeen)}",
                true => null,
            };
            var parts = new[] { VendorText, connection, seen }.OfType<string>().ToList();
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    public double IconOpacity => IsOnline == false ? 0.45 : 1;

    /// <summary>Whether the IP is known. The router lists some offline devices without one.</summary>
    public bool HasAddress => IpText.Length > 0;

    public Uri? WebUri => HasAddress ? new($"http://{IpText}/") : null;

    /// <summary>Numeric sort key so .9 sorts before .10.</summary>
    public uint SortKey => IPAddress.TryParse(IpText, out var ip) && ip.GetAddressBytes() is { Length: 4 } bytes
        ? BinaryPrimitives.ReadUInt32BigEndian(bytes)
        : uint.MaxValue;

    /// <summary>Takes in what a scan found. A MAC learned for an IP-only device becomes its identity.</summary>
    public void Update(Device found)
    {
        MacText ??= FormatMac(found);
        IpText = found.IpAddress.ToString();
        // A name lookup can miss now and then; keep the last name rather than lose it.
        if (found.HostName is not null) HostName = found.HostName;
        VendorText = VendorFor(found) ?? VendorText;
        IsThisComputer = found.IsThisComputer;
        LastSeen = found.LastSeen;
        IsOnline = true;
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>
    /// Takes in what the router knows. <paramref name="isNew"/> means only the router has seen this device, so its
    /// status is all there is to go on.
    /// </summary>
    public void Update(RouterDeviceInfo router, bool isNew)
    {
        MacText ??= router.Mac;
        if (!HasAddress && router.Ip is not null) IpText = router.Ip;
        RouterName = router.Name;
        Connection = router.Connection;
        SignalDbm = router.SignalDbm;
        if (router.LastActivity is { } activity && activity > LastSeen) LastSeen = activity;
        if (router.IsOn) IsOnline = true;
        else if (isNew) IsOnline = false;
        OnPropertyChanged(nameof(Key));
    }

    public static string? FormatMac(Device device) => device.MacAddress is null
        ? null
        : string.Join(":", device.MacAddress.GetAddressBytes().Select(b => b.ToString("X2")));

    /// <summary>The vendor for a MAC like "00:05:CD:F2:C6:80", for devices only the router has seen.</summary>
    public static string? VendorFor(string macText) =>
        PhysicalAddress.TryParse(macText.Replace(':', '-'), out var mac) && mac is not null
            ? OuiVendors.Find(mac) ?? (OuiVendors.IsLocallyAdministered(mac) ? "Private address" : null)
            : null;

    private static string? VendorFor(Device device) =>
        device.Vendor ?? (device.MacAddress is not null && OuiVendors.IsLocallyAdministered(device.MacAddress)
            ? "Private address"
            : null);

    private static string Describe(DateTimeOffset time)
    {
        var local = time.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (local.Date == today) return $"today at {local:t}";
        if (local.Date == today.AddDays(-1)) return $"yesterday at {local:t}";
        return local.Year == today.Year ? $"{local:MMM d}" : $"{local:MMM d, yyyy}";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
