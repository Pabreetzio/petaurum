namespace Petaurum.Windows.Tray;

/// <summary>The placeholder app icon (Assets/AppIcon.ico) until we have real artwork.</summary>
internal static class AppIcon
{
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
}
