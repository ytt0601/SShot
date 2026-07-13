using System.IO;
using System.Text.Json;

namespace SShot.Core.Settings;

/// <summary>
/// Plain JSON at %AppData%\SShot\settings.json, deliberately not app.config/ApplicationSettingsBase
/// - easier to hand-edit, debug, and diff in version control. The file-path constructor overload
/// exists so tests can round-trip against a throwaway path instead of the real user profile.
/// </summary>
public sealed class SettingsService
{
    private readonly string _filePath;

    public SettingsService()
        : this(DefaultFilePath)
    {
    }

    internal SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    internal static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SShot", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt JSON, or the file being momentarily locked/inaccessible (AV scan, etc.) -
            // either way, startup must proceed with defaults rather than crash before any window
            // is shown.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
