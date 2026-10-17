using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Petaurum.Windows.Tray;

/// <summary>The notification-area icon and its menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIcon(Drawing.Icon icon, Action onOpen, Action onScan, Action onExit)
    {
        var menu = new Forms.ContextMenuStrip();
        var open = menu.Items.Add("Open Petaurum", null, (_, _) => onOpen());
        open.Font = new Drawing.Font(open.Font, Drawing.FontStyle.Bold);
        menu.Items.Add("Scan now", null, (_, _) => onScan());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Petaurum",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) onOpen();
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
