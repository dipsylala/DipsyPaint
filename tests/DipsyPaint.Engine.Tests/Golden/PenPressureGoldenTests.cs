using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Pen pressure (ticket 30): a recorded pen stroke, with the pressure of each event, replayed through
/// the brushes. Pressure scales each brush's size and tapers between events. The events have whole
/// pixel positions, fixed pressures and explicit times, so the images are the same on every platform.
/// </summary>
public class PenPressureGoldenTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(8);

    /// <summary>
    /// A pen stroke as the canvas receives it: (x, y, pressure) per event, one every 8 ms. It presses
    /// lightly, bears down along a curve, eases off, and lifts, reporting no pressure (0) on release.
    /// </summary>
    internal static readonly (double X, double Y, float Pressure)[] Swoosh =
    [
        (8, 30, 0.08f), (12, 26, 0.15f), (17, 22, 0.24f), (23, 19, 0.35f), (30, 17, 0.47f), (38, 16, 0.58f),
        (46, 17, 0.68f), (54, 20, 0.77f), (61, 24, 0.84f), (67, 29, 0.9f), (73, 34, 0.94f), (79, 38, 0.95f),
        (86, 41, 0.92f), (93, 42, 0.85f), (100, 41, 0.74f), (106, 38, 0.6f), (111, 34, 0.44f), (115, 30, 0.28f),
        (118, 26, 0.14f), (119, 24, 0.05f), (119, 24, 0f),
    ];

    /// <summary>Two events far apart: the size must taper smoothly along the whole segment between them.</summary>
    internal static readonly (double X, double Y, float Pressure)[] Wedge = [(10, 64, 0.1f), (118, 64, 1f), (118, 64, 0f)];

    /// <summary>A light tap.</summary>
    internal static readonly (double X, double Y, float Pressure)[] Tap = [(20, 86, 0.3f), (20, 86, 0f)];

    [Fact]
    public void Brush() => AssertStrokes("pressure-brush", settings => new BrushTool(settings));

    [Theory]
    [InlineData("calligraphy-brush")]
    [InlineData("crayon")]
    [InlineData("marker")]
    public void ArtisticBrush(string brush) =>
        AssertStrokes("pressure-" + brush, settings => new ArtisticBrushTool(settings, ArtisticBrushGoldenTests.TipOf(brush)));

    [Fact]
    public void Airbrush() => AssertStrokes("pressure-airbrush", settings => new AirbrushTool(settings));

    [Fact]
    public void OilBrush() => AssertStrokes("pressure-oil-brush", settings => new OilBrushTool(settings));

    [Fact]
    public void WatercolourBrush() => AssertStrokes("pressure-watercolour-brush", settings => new WatercolourBrushTool(settings));

    private static void AssertStrokes(string name, Func<StrokeSettings, ITool> create)
    {
        var editor = new DocumentEditor(Document.CreateBlank(128, 96));
        editor.ActiveTool = create(new StrokeSettings(20));
        Replay(editor, Swoosh);
        editor.ActiveTool = create(new StrokeSettings(24));
        Replay(editor, Wedge);
        editor.ActiveTool = create(new StrokeSettings(16));
        Replay(editor, Tap);

        GoldenImage.AssertMatches(name, editor.Compositor);
    }

    /// <summary>Replays recorded events: the first presses, the last releases, and the rest move.</summary>
    internal static void Replay(DocumentEditor editor, (double X, double Y, float Pressure)[] events)
    {
        for (var i = 0; i < events.Length; i++)
        {
            var action = i == 0 ? PointerAction.Down : i == events.Length - 1 ? PointerAction.Up : PointerAction.Move;
            var (x, y, pressure) = events[i];
            editor.Apply(new PointerInput(action, x, y, PointerButton.Left, pressure, Time: Interval * i));
        }
    }
}
