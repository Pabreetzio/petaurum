using Microsoft.UI.Xaml;
using Petaurum.Windows.Tray;

namespace Petaurum.Windows;

/// <summary>
/// Tray-first app: the tray icon owns the lifetime, and the window is shown and hidden on demand.
/// Pass --minimized to start in the tray without opening the window.
/// </summary>
public partial class App : Application
{
    private readonly DeviceListViewModel _viewModel = new();
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

        _window = new MainWindow(_viewModel);
        _tray = new TrayIcon(ShowWindow, () => _ = _viewModel.ScanAsync(), ExitApp);

        if (!Environment.GetCommandLineArgs().Skip(1).Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            ShowWindow();

        _ = _viewModel.ScanAsync();
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
