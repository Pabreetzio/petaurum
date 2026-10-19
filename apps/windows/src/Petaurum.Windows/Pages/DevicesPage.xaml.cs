using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Petaurum.Windows.Devices;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Petaurum.Windows.Pages;

/// <summary>The device list. See specs/features/device-list.md.</summary>
public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        PreviewKeyDown += Page_PreviewKeyDown;
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

    /// <summary>The device a card or menu item belongs to, which they carry in Tag.</summary>
    private static DeviceItem DeviceOf(object sender) => (DeviceItem)((FrameworkElement)sender).Tag;

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceOf(sender).WebUri is { } uri) await Launcher.LaunchUriAsync(uri);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e) => await RenameAsync(DeviceOf(sender));

    /// <summary>F2 renames the focused card. Previewed, because the card handles key presses itself.</summary>
    private async void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.F2) return;
        if (FocusManager.GetFocusedElement(XamlRoot) is not FrameworkElement { Tag: DeviceItem device }) return;
        e.Handled = true;
        await RenameAsync(device);
    }

    private async Task RenameAsync(DeviceItem device)
    {
        var box = new TextBox
        {
            Text = device.CustomName ?? "",
            PlaceholderText = device.HostName ?? "Unknown device",
            Header = "Name",
        };
        box.SelectAll();
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename device",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    box,
                    new TextBlock
                    {
                        Text = "Leave it empty to use the name Petaurum found.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    },
                },
            },
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Rename(device, box.Text);
    }

    private async void Forget_Click(object sender, RoutedEventArgs e)
    {
        var device = DeviceOf(sender);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Forget {device.DisplayName}?",
            Content = device.CustomName is null
                ? "It will come back the next time a scan finds it."
                : "It will come back the next time a scan finds it, but without the name you gave it.",
            PrimaryButtonText = "Forget",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Forget(device);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText((string)((FrameworkElement)sender).Tag);
        Clipboard.SetContent(package);
    }
}
