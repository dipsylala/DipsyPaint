using Avalonia;
using Avalonia.Headless;
using DipsyPaint.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace DipsyPaint.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia() // real rendering, so tests can capture frames and check pixels
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
