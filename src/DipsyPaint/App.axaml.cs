using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DipsyPaint.Engine;

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
        }

        base.OnFrameworkInitializationCompleted();
    }
}
