using Avalonia;
using DipsyPaint.Diagnostics;
using Velopack;

namespace DipsyPaint;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // First, before any UI: handles Velopack's install/update/uninstall hooks (which exit), and
        // applies an update downloaded in an earlier session. Does nothing when not installed by Velopack.
        VelopackApp.Build().Run();

        BuildAvaloniaApp()
            .AfterSetup(_ => new CrashHandler(new CrashLog(CrashLog.DefaultDirectory)).Install())
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
