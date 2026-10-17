using System.Windows;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\Petaurum.Windows", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        var icon = AppIcon.Create();
        _window = new MainWindow(_viewModel) { Icon = AppIcon.ToImageSource(icon) };
        _tray = new TrayIcon(icon, ShowWindow, () => _ = _viewModel.ScanAsync(), ExitApp);

        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            ShowWindow();

        _ = _viewModel.ScanAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        if (_window is not null)
        {
            _window.AllowClose = true;
            _window.Close();
        }
        Shutdown();
    }
}
