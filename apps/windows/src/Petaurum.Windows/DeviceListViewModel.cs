using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using Petaurum.Windows.Devices;
using Petaurum.Windows.Discovery;
using Petaurum.Windows.Router;

namespace Petaurum.Windows;

public sealed class DeviceListViewModel : INotifyPropertyChanged
{
    private readonly NetworkScanner _scanner = new();
    private IReadOnlyDictionary<string, RouterDeviceInfo> _router = new Dictionary<string, RouterDeviceInfo>();
    private DateTimeOffset? _lastScan;
    private bool _isScanning;
    private string _status;

    /// <summary>Shows remembered devices straight away; a scan then updates them.</summary>
    public DeviceListViewModel()
    {
        var (lastScan, devices) = DeviceStore.Load();
        _lastScan = lastScan;
        foreach (var device in Sorted(devices)) Devices.Add(device);
        _status = lastScan is { } time
            ? $"{Devices.Count} remembered devices · last scanned {time.ToLocalTime():g}"
            : "Not scanned yet";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceItem> Devices { get; } = [];

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (Set(ref _isScanning, value)) OnPropertyChanged(nameof(CanScan));
        }
    }

    public bool CanScan => !IsScanning;

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>Runs a scan unless one is already running. Must be called on the UI thread.</summary>
    public async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        Status = "Scanning…";
        try
        {
            var progress = new Progress<string>(message => Status = message);
            // The router can keep a stale record for an IP another device now has; prefer the one that's on.
            var routerNames = _router.Values
                .Where(r => r.Ip is not null && r.Name is not null)
                .GroupBy(r => IPAddress.Parse(r.Ip!))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.IsOn).ThenByDescending(r => r.LastActivity).First().Name!);
            var found = await _scanner.ScanAsync(progress, routerNames, CancellationToken.None);
            Merge(found);
            MergeRouter();
            Reorder();
            _lastScan = DateTimeOffset.Now;
            Save();
            UpdateStatus();
        }
        catch (Exception e)
        {
            // Keep the previous results visible.
            Status = $"Scan failed: {e.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Takes in what the router knows about each device (from its stored pages, so this is instant) and shows it on
    /// the cards. See specs/features/router.md, "Device list".
    /// </summary>
    public void ApplyRouter(IReadOnlyDictionary<string, RouterDeviceInfo> router)
    {
        _router = router;
        MergeRouter();
        Reorder();
        Save();
        if (!IsScanning && _lastScan is not null) UpdateStatus();
    }

    private void UpdateStatus()
    {
        var online = Devices.Count(d => d.IsOnline == true);
        var scanned = $"scanned at {_lastScan!.Value.ToLocalTime():t}";
        Status = online == Devices.Count
            ? $"{online} devices · {scanned}"
            : $"{online} online, {Devices.Count - online} offline · {scanned}";
    }

    public void Rename(DeviceItem device, string? name)
    {
        device.CustomName = name;
        Save();
    }

    public void Forget(DeviceItem device)
    {
        Devices.Remove(device);
        Save();
    }

    /// <summary>Merges scan results into the remembered devices by identity. See "Memory".</summary>
    private void Merge(IReadOnlyList<Device> found)
    {
        var byKey = Devices.GroupBy(d => d.Key).ToDictionary(g => g.Key, g => g.First());
        var seen = new HashSet<DeviceItem>();
        foreach (var device in found)
        {
            var mac = DeviceItem.FormatMac(device);
            var ip = device.IpAddress.ToString();
            // A device first seen by IP alone is the same device once its MAC shows up at that IP.
            var item = byKey.GetValueOrDefault(DeviceItem.KeyFor(mac, ip))
                       ?? (mac is null ? null : byKey.GetValueOrDefault(DeviceItem.KeyFor(null, ip)));
            if (item is null)
            {
                item = new DeviceItem(mac, ip, device.LastSeen);
                byKey[item.Key] = item;
            }
            item.Update(device);
            seen.Add(item);
        }
        foreach (var item in byKey.Values.Where(d => !seen.Contains(d)))
            item.IsOnline = false; // MergeRouter brings back any the router says are on.
        foreach (var item in byKey.Values.Distinct().Where(d => !Devices.Contains(d)))
            Devices.Add(item);
    }

    /// <summary>Matches router info to devices by MAC, and adds devices only the router knows about.</summary>
    private void MergeRouter()
    {
        var byMac = Devices.Where(d => d.MacText is not null).GroupBy(d => d.MacText!).ToDictionary(g => g.Key, g => g.First());
        foreach (var info in _router.Values)
        {
            var isNew = false;
            if (!byMac.TryGetValue(info.Mac, out var item))
            {
                // A device first seen by IP alone is the same device once its MAC shows up at that IP.
                item = Devices.FirstOrDefault(d => d.MacText is null && info.Ip is not null && d.IpText == info.Ip);
                if (item is null)
                {
                    isNew = true;
                    item = new DeviceItem(info.Mac, info.Ip ?? "", info.LastActivity ?? DateTimeOffset.Now)
                    {
                        VendorText = DeviceItem.VendorFor(info.Mac),
                    };
                    Devices.Add(item);
                }
                byMac[info.Mac] = item;
            }
            item.Update(info, isNew);
        }
    }

    /// <summary>Sorts in place, so cards that stay keep their state (focus, open menus) instead of being rebuilt.</summary>
    private void Reorder()
    {
        var sorted = Sorted(Devices.Distinct());
        for (var i = 0; i < sorted.Count; i++)
        {
            var index = Devices.IndexOf(sorted[i]);
            if (index == i) continue;
            if (index < 0) Devices.Insert(i, sorted[i]);
            else Devices.Move(index, i);
        }
        while (Devices.Count > sorted.Count) Devices.RemoveAt(Devices.Count - 1);
    }

    /// <summary>Online devices first, each group by IP address.</summary>
    private static List<DeviceItem> Sorted(IEnumerable<DeviceItem> devices) =>
        devices.OrderByDescending(d => d.IsOnline != false).ThenBy(d => d.SortKey).ToList();

    private void Save()
    {
        try
        {
            DeviceStore.Save(_lastScan, Devices);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save devices: {e.Message}";
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
