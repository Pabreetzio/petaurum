using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml.Controls;

namespace Petaurum.Windows.Tray;

/// <summary>The notification-area icon and its menu, a Fluent MenuFlyout like the system's own tray menus.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayIcon(Action onOpen, Action onScan, Action onExit)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(Item("Open Petaurum", "\uE8A7", onOpen));
        menu.Items.Add(Item("Scan now", "\uE72C", onScan));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Exit", "\uE7E8", onExit));

        _icon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(AppIcon.Path),
            ToolTipText = "Petaurum",
            ContextFlyout = menu,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            NoLeftClickDelay = true,
            LeftClickCommand = new Command(onOpen),
        };
        // Efficiency mode would throttle scans while the window is hidden.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose() => _icon.Dispose();

    private static MenuFlyoutItem Item(string text, string glyph, Action onClick) =>
        new() { Text = text, Icon = new FontIcon { Glyph = glyph }, Command = new Command(onClick) };

    private sealed class Command(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
