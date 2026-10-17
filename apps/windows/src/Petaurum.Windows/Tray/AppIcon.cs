using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace Petaurum.Windows.Tray;

/// <summary>Placeholder app icon drawn at runtime until we have real artwork.</summary>
internal static class AppIcon
{
    private static readonly Drawing.Color Teal = Drawing.Color.FromArgb(0x0F, 0x76, 0x6E);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Drawing.Icon Create()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var background = new Drawing.SolidBrush(Teal);
            g.FillEllipse(background, 1, 1, 30, 30);
            using var font = new Drawing.Font("Segoe UI", 20, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var format = new Drawing.StringFormat
            {
                Alignment = Drawing.StringAlignment.Center,
                LineAlignment = Drawing.StringAlignment.Center,
            };
            g.DrawString("P", font, Drawing.Brushes.White, new Drawing.RectangleF(0, 1, 32, 32), format);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static ImageSource ToImageSource(Drawing.Icon icon) =>
        Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
}
