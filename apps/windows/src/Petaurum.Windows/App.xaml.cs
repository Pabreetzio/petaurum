using System.ComponentModel;
using Microsoft.UI.Xaml;
using Petaurum.Windows.Router;
using Petaurum.Windows.Tray;

namespace Petaurum.Windows;

/// <summary>
/// Tray-first app: the tray icon owns the lifetime, and the window is shown and hidden on demand.
/// Pass --minimized to start in the tray without opening the window.
/// </summary>
public partial class App : Application
{
    private DeviceListViewModel _viewModel = null!;
    private readonly RouterViewModel _router = new();
    private Mutex? _singleInstance;
    private TrayIcon? _tray;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstance = new Mutex(true, @"Local\Petaurum.Windows", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Exit();
            return;
        }

        // Keep running in the tray while the window is hidden.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        // Created here, not as a field, so loading remembered devices can't fail before the single-instance check.
        _viewModel = new DeviceListViewModel();
        ShowRouterInfoOnDevices();
        _window = new MainWindow(_viewModel, _router);
        _tray = new TrayIcon(ShowWindow, () => _ = _viewModel.ScanAsync(), ExitApp);

        if (!Environment.GetCommandLineArgs().Skip(1).Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            ShowWindow();

        _ = _viewModel.ScanAsync();
        _ = _router.StartAsync();
    }

    /// <summary>Whenever the router's Device List or Home Network page is loaded or refreshed, update the device cards.</summary>
    private void ShowRouterInfoOnDevices()
    {
        var deviceList = _router.Pages.First(p => p.Path == "devices.ha");
        var homeNetwork = _router.Pages.First(p => p.Path == "lanstatistics.ha");
        void Apply(object? sender, PropertyChangedEventArgs e)
        {
            // ReadAt too: it's set just after Data, and decides whether the router's on/off status is fresh.
            if (e.PropertyName is nameof(RouterPageViewModel.Data) or nameof(RouterPageViewModel.ReadAt))
                _viewModel.ApplyRouter(RouterDevices.From(deviceList.Data, deviceList.ReadAt, homeNetwork.Data));
        }
        deviceList.PropertyChanged += Apply;
        homeNetwork.PropertyChanged += Apply;
    }

    private void ShowWindow() => _window?.ShowAndActivate();

    /// <summary>Called from the tray menu, so let the menu finish before tearing down the tray icon.</summary>
    private void ExitApp() => _window?.DispatcherQueue.TryEnqueue(() =>
    {
        _window.CloseForExit();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Exit();
    });
}
