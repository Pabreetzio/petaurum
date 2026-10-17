using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Petaurum.Windows.Discovery;

namespace Petaurum.Windows;

public sealed class DeviceListViewModel : INotifyPropertyChanged
{
    private readonly NetworkScanner _scanner = new();
    private bool _isScanning;
    private string _status = "Not scanned yet";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Device> Devices { get; } = [];

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
            var devices = await _scanner.ScanAsync(progress, CancellationToken.None);

            Devices.Clear();
            foreach (var device in devices) Devices.Add(device);
            Status = $"{devices.Count} devices · scanned at {DateTime.Now:t}";
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
