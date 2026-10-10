using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint;

/// <summary>
/// Cold-start measurement and smoke test for published builds (ADR-0018). When the environment
/// variable <see cref="Variable"/> names a file, the app appends one line to it once the main window
/// has rendered its first frame, then exits: <c>startup_ms=N codecs=ok</c>, where N is the time
/// from process start, and the codecs part round-trips the document through PNG and JPEG with
/// ImageSharp (or reports <c>codecs=failed</c>). Without the variable it does nothing.
/// </summary>
internal static class StartupProbe
{
    public const string Variable = "DIPSYPAINT_STARTUP_PROBE";

    public static void Attach(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        var path = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // The second animation frame callback runs after the first frame has been rendered.
        window.Opened += (_, _) => window.RequestAnimationFrame(_ => window.RequestAnimationFrame(_ =>
        {
            var elapsed = DateTime.Now - Process.GetCurrentProcess().StartTime;
            File.AppendAllText(path, $"startup_ms={elapsed.TotalMilliseconds:F0} codecs={CheckCodecs(window.Document)}{Environment.NewLine}");
            desktop.Shutdown();
        }));
    }

    private static string CheckCodecs(Document document)
    {
        try
        {
            foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg })
            {
                using var stream = new MemoryStream();
                ImageCodec.Export(new Compositor(document), stream, format);
                stream.Position = 0;
                var copy = ImageCodec.Open(stream);
                if (copy.Width != document.Width || copy.Height != document.Height)
                {
                    return $"failed:{format}-size";
                }
            }

            return "ok";
        }
        catch (Exception e)
        {
            return $"failed:{e.GetType().Name}";
        }
    }
}
