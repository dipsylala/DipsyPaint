using Avalonia;
using Avalonia.Headless;
using DipsyPaint.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace DipsyPaint.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
