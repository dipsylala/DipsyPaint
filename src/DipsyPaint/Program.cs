using Avalonia;
using DipsyPaint.Diagnostics;

namespace DipsyPaint;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp()
            .AfterSetup(_ => new CrashHandler(new CrashLog(CrashLog.DefaultDirectory)).Install())
            .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
