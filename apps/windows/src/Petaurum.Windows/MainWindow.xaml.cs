using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Petaurum.Windows.Pages;
using Petaurum.Windows.Tray;
using Windows.Graphics;

namespace Petaurum.Windows;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow(DeviceListViewModel viewModel)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TitleBarIcon.ImageSource = new BitmapImage(new Uri(AppIcon.Path));
        AppWindow.SetIcon(AppIcon.Path);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 480;
            presenter.PreferredMinimumHeight = 360;
        }
        ResizeAndCenter(960, 640);

        // Closing hides to the tray unless the app is exiting.
        AppWindow.Closing += (sender, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            sender.Hide();
        };

        ContentFrame.Navigate(typeof(DevicesPage), viewModel);
    }

    public void ShowAndActivate()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        AppWindow.Show();
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>Sizes the window in effective pixels and centres it in the work area.</summary>
    private void ResizeAndCenter(int width, int height)
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var w = Math.Min((int)(width * scale), workArea.Width);
        var h = Math.Min((int)(height * scale), workArea.Height);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - w) / 2,
            workArea.Y + (workArea.Height - h) / 2,
            w, h));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
