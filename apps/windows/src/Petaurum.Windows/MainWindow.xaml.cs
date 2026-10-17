using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Petaurum.Windows;

public partial class MainWindow : Window
{
    private readonly DeviceListViewModel _viewModel;

    public MainWindow(DeviceListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Closing hides to the tray unless the app is exiting.</summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private async void ScanNow_Click(object sender, RoutedEventArgs e) => await _viewModel.ScanAsync();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
