using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Airbrush, oil brush and watercolour brush strokes (ticket 29): several sizes, both buttons, a
/// translucent stroke that goes back over itself and over a band of paint, a click, and for the
/// airbrush a spray held still. The spray and textures are seeded from each stroke's start, and
/// events are timed explicitly, so the images are the same on every platform.
/// </summary>
public class ArtisticBrushes2GoldenTests
{
    private static readonly Rgba Red = new(220, 30, 30, 255);
    private static readonly Rgba Blue = new(30, 60, 200, 255);
    private static readonly Rgba Yellow = new(250, 200, 0, 255);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void Airbrush() => AssertStrokes("airbrush", settings => new AirbrushTool(settings), holdStill: true);

    [Fact]
    public void OilBrush() => AssertStrokes("oil-brush", settings => new OilBrushTool(settings));

    [Fact]
    public void WatercolourBrush() => AssertStrokes("watercolour-brush", settings => new WatercolourBrushTool(settings));

    private static void AssertStrokes(string name, Func<StrokeSettings, ITool> create, bool holdStill = false)
    {
        var editor = new DocumentEditor(Document.CreateBlank(128, 96)) { PrimaryColor = Yellow, SecondaryColor = Red };
        editor.ActiveTool = new BrushTool(new StrokeSettings(14));
        Stroke(editor, PointerButton.Left, Line((100, 4), (100, 92), 22)); // a band underneath
        editor.PrimaryColor = Blue;

        Brush(editor, create, 3, 100, PointerButton.Left, Line((6, 6), (120, 8), 38));
        Brush(editor, create, 8, 100, PointerButton.Left, Line((6, 18), (120, 22), 38));
        Brush(editor, create, 16, 100, PointerButton.Left, [.. Line((10, 40), (60, 34), 17), .. Line((60, 34), (118, 46), 19)]);
        Brush(editor, create, 10, 50, PointerButton.Right,
            [.. Line((8, 60), (70, 60), 20), .. Line((70, 60), (12, 80), 20), .. Line((12, 80), (118, 80), 30)]); // goes back over itself
        Brush(editor, create, 12, 100, PointerButton.Left, [(84, 64)]); // a click
        if (holdStill)
        {
            Brush(editor, create, 20, 100, PointerButton.Left, [.. Enumerable.Repeat((40.0, 90.0), 12)]);
        }

        GoldenImage.AssertMatches(name, editor.Compositor);
    }

    private static void Brush(
        DocumentEditor editor, Func<StrokeSettings, ITool> create, int size, int opacity, PointerButton button, (double X, double Y)[] points)
    {
        editor.ActiveTool = create(new StrokeSettings(size) { Opacity = opacity });
        Stroke(editor, button, points);
    }

    /// <summary>A stroke through <paramref name="points"/>, one event every 16 ms.</summary>
    private static void Stroke(DocumentEditor editor, PointerButton button, (double X, double Y)[] points)
    {
        var time = TimeSpan.Zero;
        editor.Apply(new PointerInput(PointerAction.Down, points[0].X, points[0].Y, button, Time: time));
        foreach (var (x, y) in points.Skip(1))
        {
            time += Interval;
            editor.Apply(new PointerInput(PointerAction.Move, x, y, button, Time: time));
        }

        editor.Apply(new PointerInput(PointerAction.Up, points[^1].X, points[^1].Y, button, Time: time));
    }

    private static (double X, double Y)[] Line((double X, double Y) from, (double X, double Y) to, int steps) =>
        [.. Enumerable.Range(0, steps + 1).Select(i => (from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps))];
}
