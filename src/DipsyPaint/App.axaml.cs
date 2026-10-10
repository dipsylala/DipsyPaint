using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DipsyPaint.Engine;
using DipsyPaint.Updates;

namespace DipsyPaint;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(Document.CreateBlank());
            desktop.MainWindow = window;
            StartupProbe.Attach(desktop, window);

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
}
