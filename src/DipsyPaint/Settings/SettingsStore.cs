using System.Text.Json;

namespace DipsyPaint.Settings;

/// <summary>
/// Reads and writes the per-user settings file, a small JSON file that stays on this computer and
/// is never sent anywhere (ADR-0017). A missing, unreadable or corrupt file reads as the defaults.
/// </summary>
public sealed class SettingsStore(string path)
{
    /// <summary>Larger files are not a settings file DipsyPaint wrote, so they read as the defaults.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>
    /// The per-user settings file: <c>DipsyPaint/settings.json</c> under local app data, the same
    /// folder as the crash logs.
    /// </summary>
    public static string DefaultPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "DipsyPaint",
        "settings.json");

    public string Path { get; } = path;

    /// <summary>The saved settings, or new (default) settings if there are none or they cannot be read.</summary>
    public UserSettings Load()
    {
        try
        {
            var file = new FileInfo(Path);
            if (!file.Exists || file.Length > MaxFileBytes)
            {
                return new UserSettings();
            }

            using var stream = file.OpenRead();
            return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.UserSettings) ?? new UserSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new UserSettings();
        }
    }

    /// <summary>
    /// Saves <paramref name="settings"/>, replacing the file only once the new one is fully written.
    /// Returns false if it could not be saved; the app carries on with the settings in memory.
    /// </summary>
    public bool Save(UserSettings settings)
    {
        var temp = Path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJsonContext.Default.UserSettings));
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
