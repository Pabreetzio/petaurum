using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Petaurum.Windows.Storage;

/// <summary>JSON files under %LOCALAPPDATA%\Petaurum. Data stays on this computer.</summary>
internal static class LocalStore
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Petaurum");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Reads a stored file, or returns null if it's missing or unreadable.</summary>
    public static T? Read<T>(string relativePath) where T : class
    {
        var path = Path.Combine(Folder, relativePath);
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Ignoring unreadable {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>Writes a file atomically, so a crash mid-write never leaves half a file.</summary>
    public static void Write<T>(string relativePath, T value)
    {
        var path = Path.Combine(Folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var stream = File.Create(temporary))
            JsonSerializer.Serialize(stream, value, Options);
        File.Move(temporary, path, overwrite: true);
    }
}
