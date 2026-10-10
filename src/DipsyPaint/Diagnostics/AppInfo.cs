using System.Reflection;
using System.Runtime.InteropServices;

namespace DipsyPaint.Diagnostics;

/// <summary>The app version and platform details that crash logs and problem reports include.</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static string Os { get; } = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    public static string Runtime { get; } = $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})";
}
