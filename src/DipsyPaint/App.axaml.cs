using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DipsyPaint.Diagnostics;
using DipsyPaint.Engine;
using DipsyPaint.Recovery;
using DipsyPaint.Settings;
using DipsyPaint.Updates;

namespace DipsyPaint;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsStore(SettingsStore.DefaultPath);
            var window = new MainWindow(Document.CreateBlank(), settings);
            desktop.MainWindow = window;
            StartupProbe.Attach(desktop, window);
            if (!StartupProbe.IsRequested)
            {
                StartCrashRecovery(window, settings);
            }

            // The update check, the app's only automatic network request (ADR-0017): installed copies
            // only, once the window is open, and never in a probe run.
            if (!StartupProbe.IsRequested)
            {
                window.Opened += async (_, _) =>
                {
                    if (VelopackUpdater.CreateIfInstalled() is { } updater)
                    {
                        await window.Updates.CheckAsync(updater);
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Recovery snapshots (ADR-0013); never in a probe run, which must not touch the user's snapshots.
    // Without a writable recovery folder the app runs on unprotected.
    private static void StartCrashRecovery(MainWindow window, SettingsStore settings)
    {
        CrashRecovery recovery;
        try
        {
            recovery = new CrashRecovery(
                window,
                new RecoveryStore(RecoveryStore.DefaultDirectory),
                RecoverySettings.IntervalOf(settings.Load().Recovery),
                crashes: CrashHandler.Installed);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        window.Opened += async (_, _) => await recovery.OfferRestoreAsync();
    }
}
