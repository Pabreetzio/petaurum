using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Petaurum.Windows.Router;
using Windows.System;

namespace Petaurum.Windows.Pages;

/// <summary>One router page, rebuilt from its data. See specs/features/router.md.</summary>
public sealed partial class RouterPageView : Page
{
    public RouterPageView()
    {
        InitializeComponent();
    }

    public RouterViewModel Router { get; private set; } = null!;
    public RouterPageViewModel Page { get; private set; } = null!;

    /// <summary>The navigation parameter: which page, and the router it belongs to.</summary>
    public sealed record Parameter(RouterViewModel Router, RouterPageViewModel Page);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        (Router, Page) = (Parameter)e.Parameter;
        Bindings.Update();
        Router.Open(Page);
        base.OnNavigatedTo(e);
    }

    public static Visibility VisibleIf(string? text) =>
        string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static bool Not(bool value) => !value;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Router.RefreshAsync(Page);

    private async void OpenInBrowser_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(Router.PageUri(Page)));

    private void ForgetAccessCode_Click(object sender, RoutedEventArgs e) => Router.ForgetAccessCode();

    private async void EnterAccessCode_Click(object sender, RoutedEventArgs e)
    {
        var box = new PasswordBox { Header = "Device access code", MaxLength = 32 };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Sign in to the router",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter the device access code printed on the router's label. Petaurum stores it in "
                               + "Windows Credential Locker and only sends the router a hash of it.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    box,
                },
            },
            PrimaryButtonText = "Sign in",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && box.Password.Length > 0)
            await Router.SetAccessCodeAsync(box.Password);
    }
}
