using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Petaurum.Windows.Pages;

/// <summary>The device list. See specs/features/device-list.md.</summary>
public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
    }

    public DeviceListViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (DeviceListViewModel)e.Parameter;
        Bindings.Update();
        base.OnNavigatedTo(e);
    }

    /// <summary>Segoe Fluent Icons glyph for a device. Becomes per device type once devices have one.</summary>
    public static string GlyphFor(bool isThisComputer) => isThisComputer ? "\uE7F8" : "\uE839";

    public static bool HasValue(string? text) => !string.IsNullOrEmpty(text);

    public static Visibility VisibleIf(string? text) => HasValue(text) ? Visibility.Visible : Visibility.Collapsed;

    private async void ScanNow_Click(object sender, RoutedEventArgs e) => await ViewModel.ScanAsync();

    /// <summary>Opens the device's web UI. The card and its menu item carry the URI in Tag.</summary>
    private async void Open_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync((Uri)((FrameworkElement)sender).Tag);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText((string)((FrameworkElement)sender).Tag);
        Clipboard.SetContent(package);
    }
}
