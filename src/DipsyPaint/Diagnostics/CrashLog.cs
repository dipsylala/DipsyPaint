using System.Globalization;
using System.Text;

namespace DipsyPaint.Diagnostics;

/// <summary>
/// Plain-text crash logs in a folder, newest <see cref="MaxLogs"/> kept. A log holds the version, the
/// platform and the exception with its stack trace, scrubbed of the home folder and user name. It never
/// contains image data, and nothing ever sends it anywhere (ADR-0017).
/// </summary>
public sealed class CrashLog(string directory, int maxLogs = CrashLog.DefaultMaxLogs)
{
    public const int DefaultMaxLogs = 10;

    private const string Prefix = "crash-";
    private const string Extension = ".txt";

    /// <summary>
    /// The per-user crash-log folder: <c>DipsyPaint/CrashLogs</c> under local app data, that is
    /// <c>%LOCALAPPDATA%</c> on Windows, <c>~/Library/Application Support</c> on macOS and
    /// <c>$XDG_DATA_HOME</c> (default <c>~/.local/share</c>) on Linux.
    /// </summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "DipsyPaint",
        "CrashLogs");

    public string Directory { get; } = directory;

    public int MaxLogs { get; } = maxLogs;

    /// <summary>The scrubbed text of a crash log for <paramref name="exception"/>.</summary>
    public static string Format(Exception exception, string source, DateTimeOffset time)
    {
        var text = new StringBuilder()
            .AppendLine("DipsyPaint crash log")
            .AppendLine(CultureInfo.InvariantCulture, $"Time: {time.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fff} UTC")
            .AppendLine(CultureInfo.InvariantCulture, $"Version: {AppInfo.Version}")
            .AppendLine(CultureInfo.InvariantCulture, $"OS: {AppInfo.Os}")
            .AppendLine(CultureInfo.InvariantCulture, $".NET: {AppInfo.Runtime}")
            .AppendLine(CultureInfo.InvariantCulture, $"Source: {source}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .ToString();
        return Scrubber.Scrub(text);
    }

    /// <summary>Writes a log for <paramref name="exception"/>, drops the oldest beyond <see cref="MaxLogs"/>, and returns its path.</summary>
    public string Write(Exception exception, string source, DateTimeOffset time)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, $"{Prefix}{time.ToUniversalTime():yyyyMMdd-HHmmss-fff}{Extension}");
        File.WriteAllText(path, Format(exception, source, time));
        foreach (var old in Logs().Skip(MaxLogs))
        {
            File.Delete(old);
        }

        return path;
    }

    /// <summary>The newest log's text, or null if there is none.</summary>
    public string? ReadNewest()
    {
        try
        {
            return Logs().FirstOrDefault() is { } path ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Log paths, newest first (names sort by time).</summary>
    private IEnumerable<string> Logs() =>
        System.IO.Directory.Exists(Directory)
            ? System.IO.Directory.GetFiles(Directory, Prefix + "*" + Extension).OrderDescending(StringComparer.Ordinal)
            : [];
}
