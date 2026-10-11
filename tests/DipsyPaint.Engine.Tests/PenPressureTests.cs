using DipsyPaint.Engine.Tests.Golden;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests;

public class PenPressureTests
{
    public static TheoryData<string> Brushes =>
        ["brush", "calligraphy-brush", "calligraphy-pen", "crayon", "marker", "natural-pencil", "airbrush", "oil-brush", "watercolour-brush"];

    private static ITool Create(string brush, StrokeSettings settings) => brush switch
    {
        "brush" => new BrushTool(settings),
        "airbrush" => new AirbrushTool(settings),
        "oil-brush" => new OilBrushTool(settings),
        "watercolour-brush" => new WatercolourBrushTool(settings),
        _ => new ArtisticBrushTool(settings, ArtisticBrushGoldenTests.TipOf(brush)),
    };

    private static Rgba[] Draw(Func<ITool> create, (double X, double Y, float Pressure)[] events)
    {
        var editor = new DocumentEditor(Document.CreateBlank(128, 96)) { ActiveTool = create() };
        PenPressureGoldenTests.Replay(editor, events);
        return editor.Compositor.Pixels.ToArray();
    }

    /// <summary>Rows of column <paramref name="x"/> that are not white.</summary>
    private static int PaintedRows(Rgba[] pixels, int x) => Enumerable.Range(0, 96).Count(y => pixels[y * 128 + x] != Rgba.White);

    [Theory]
    [InlineData(20, 1f, 20)]
    [InlineData(20, 0.5f, 10)]
    [InlineData(20, 0.01f, StrokeSettings.MinSize)] // never below the smallest size
    [InlineData(20, 0f, StrokeSettings.MinSize)]
    [InlineData(20, float.NaN, 20)]                 // no pressure
    [InlineData(20, 1.5f, 20)]
    public void ScaleSize_IsInProportionToPressure(double size, float pressure, double expected) =>
        Assert.Equal(expected, PenPressure.ScaleSize(size, pressure));

    [Fact]
    public void Nearest_WithoutTaper_IsTheNearestPointOfTheSegment()
    {
        Assert.Equal(0.25, PenPressure.Nearest(2.5, 7, 10, 0, 0));
        Assert.Equal(1, PenPressure.Nearest(15, 0, 10, 0, 0));
        Assert.Equal(0, PenPressure.Nearest(-3, 4, 10, 0, 0));
    }

    [Fact]
    public void Nearest_WithTaper_LeansTowardsTheLargerEnd()
    {
        var growing = PenPressure.Nearest(5, 4, 10, 0, 4);
        var shrinking = PenPressure.Nearest(5, 4, 10, 0, -4);

        Assert.True(growing > 0.5, $"{growing}");
        Assert.True(shrinking < 0.5, $"{shrinking}");
        Assert.Equal(1, PenPressure.Nearest(5, 4, 3, 0, 4)); // the end disc contains the start disc
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void FullPressure_PaintsExactlyAsAMouse(string brush)
    {
        var mouse = PenPressureGoldenTests.Swoosh.Select(e => (e.X, e.Y, 1f)).ToArray();

        var withPressure = Draw(() => Create(brush, new StrokeSettings(20)), mouse);
        var editor = new DocumentEditor(Document.CreateBlank(128, 96)) { ActiveTool = Create(brush, new StrokeSettings(20)) };
        for (var i = 0; i < mouse.Length; i++)
        {
            var action = i == 0 ? PointerAction.Down : i == mouse.Length - 1 ? PointerAction.Up : PointerAction.Move;
            editor.Apply(new PointerInput(action, mouse[i].X, mouse[i].Y, Time: TimeSpan.FromMilliseconds(8 * i)));
        }

        Assert.Equal(editor.Compositor.Pixels.ToArray(), withPressure);
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void LightPressure_PaintsThinnerThanFullPressure(string brush)
    {
        (double X, double Y, float Pressure)[] Line(float pressure) =>
            [.. Enumerable.Range(0, 28).Select(i => (10.0 + 4 * i, 48.0, pressure)), (118, 48, pressure)];

        var light = Height(Draw(() => Create(brush, new StrokeSettings(30)), Line(0.3f)));
        var full = Height(Draw(() => Create(brush, new StrokeSettings(30)), Line(1f)));

        Assert.True(light < full * 0.6, $"{light} vs {full}");
    }

    /// <summary>How many rows have any paint.</summary>
    private static int Height(Rgba[] pixels) => Enumerable.Range(0, 96).Count(y => Enumerable.Range(0, 128).Any(x => pixels[y * 128 + x] != Rgba.White));

    [Theory]
    [MemberData(nameof(Brushes))]
    public void Release_KeepsThePressureBeforeIt(string brush)
    {
        // Pens report no pressure as they lift off; that must not shrink or grow the end of the stroke.
        var events = PenPressureGoldenTests.Swoosh.ToArray();
        var lastPressure = events[^2].Pressure;
        var releasedAtFull = events.ToArray();
        releasedAtFull[^1] = releasedAtFull[^1] with { Pressure = 1f };
        var releasedAtLast = events.ToArray();
        releasedAtLast[^1] = releasedAtLast[^1] with { Pressure = lastPressure };

        var expected = Draw(() => Create(brush, new StrokeSettings(20)), releasedAtLast);

        Assert.Equal(expected, Draw(() => Create(brush, new StrokeSettings(20)), events));
        Assert.Equal(expected, Draw(() => Create(brush, new StrokeSettings(20)), releasedAtFull));
    }

    [Theory]
    [InlineData("brush")]
    [InlineData("marker")]
    public void PressureBetweenEvents_TapersSmoothly(string brush)
    {
        var pixels = Draw(() => Create(brush, new StrokeSettings(24)), PenPressureGoldenTests.Wedge);

        // From the light end to the heavy end the mark only widens, a little at a time, with no steps.
        var widths = Enumerable.Range(14, 100).Select(x => PaintedRows(pixels, x)).ToArray();
        Assert.True(widths[0] < widths[^1] / 3, $"{widths[0]} vs {widths[^1]}");
        for (var i = 1; i < widths.Length; i++)
        {
            Assert.InRange(widths[i] - widths[i - 1], 0, 2);
        }
    }

    [Fact]
    public void PencilAndEraser_IgnorePressure()
    {
        (double X, double Y, float Pressure)[] Line(float pressure) => [(10, 48, pressure), (60, 30, pressure), (118, 48, pressure)];

        Assert.Equal(Draw(() => new PencilTool(), Line(1f)), Draw(() => new PencilTool(), Line(0.2f)));

        Rgba[] Erase(float pressure)
        {
            var editor = new DocumentEditor(Document.CreateBlank(128, 96)) { ActiveTool = new BrushTool(new StrokeSettings(40)) };
            Script.Stroke(editor, PointerButton.Left, (10, 48), (118, 48));
            editor.ActiveTool = new EraserTool(new StrokeSettings(12));
            PenPressureGoldenTests.Replay(editor, Line(pressure));
            return editor.Compositor.Pixels.ToArray();
        }

        Assert.Equal(Erase(1f), Erase(0.2f));
    }
}
