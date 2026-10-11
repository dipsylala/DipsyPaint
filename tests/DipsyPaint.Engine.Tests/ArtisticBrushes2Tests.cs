using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests;

/// <summary>The airbrush, oil brush and watercolour brush (ticket 29).</summary>
public class ArtisticBrushes2Tests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    public static TheoryData<string> Brushes => ["airbrush", "oil", "watercolour"];

    private static ITool CreateTool(string name, StrokeSettings settings) => name switch
    {
        "airbrush" => new AirbrushTool(settings),
        "oil" => new OilBrushTool(settings),
        _ => new WatercolourBrushTool(settings),
    };

    private static DocumentEditor CreateEditor(ITool tool, int width = 64, int height = 64) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = tool, PrimaryColor = Red, SecondaryColor = Blue };

    private static IPixelAccess Pixels(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = Pixels(editor);
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    private static List<(int X, int Y, Rgba Color)> Painted(DocumentEditor editor)
    {
        var pixels = Pixels(editor);
        return [.. from y in Enumerable.Range(0, pixels.Height)
                   from x in Enumerable.Range(0, pixels.Width)
                   let color = pixels.GetPixel(x, y)
                   where color != Rgba.White
                   select (x, y, color)];
    }

    /// <summary>A stroke through <paramref name="points"/> with events <paramref name="interval"/> apart.</summary>
    private static void TimedStroke(DocumentEditor editor, TimeSpan interval, params (double X, double Y)[] points)
    {
        var time = TimeSpan.Zero;
        editor.Apply(new PointerInput(PointerAction.Down, points[0].X, points[0].Y, Time: time));
        foreach (var (x, y) in points.Skip(1))
        {
            time += interval;
            editor.Apply(new PointerInput(PointerAction.Move, x, y, Time: time));
        }

        editor.Apply(new PointerInput(PointerAction.Up, points[^1].X, points[^1].Y, Time: time));
    }

    private static (double X, double Y)[] Line((double X, double Y) from, (double X, double Y) to, int steps) =>
        [.. Enumerable.Range(0, steps + 1).Select(i => (from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps))];

    [Theory]
    [MemberData(nameof(Brushes))]
    public void EachBrush_PaintsInThePrimaryColourWithTheLeftButton_AndTheSecondaryWithTheRight(string brush)
    {
        foreach (var (button, color) in new[] { (PointerButton.Left, Red), (PointerButton.Right, Blue) })
        {
            var editor = CreateEditor(CreateTool(brush, new StrokeSettings(12)));

            Script.Stroke(editor, button, Line((10, 32), (54, 32), 22));

            var painted = Painted(editor);
            Assert.NotEmpty(painted);
            // Every painted pixel is the brush colour over white, with no other hue.
            Assert.All(painted, p => Assert.True(
                color == Red ? p.Color.R == 255 && p.Color.G == p.Color.B : p.Color.B == 255 && p.Color.R == p.Color.G, $"{p}"));
        }
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void TheSize_SetsHowWideTheStrokeIs(string brush)
    {
        foreach (var size in new[] { 6, 20 })
        {
            var editor = CreateEditor(CreateTool(brush, new StrokeSettings(size)));

            TimedStroke(editor, TimeSpan.FromMilliseconds(100), Line((16.5, 32.5), (48.5, 32.5), 32));

            var rows = Painted(editor).Select(p => p.Y).Distinct().ToList();
            Assert.InRange(rows.Max() - rows.Min() + 1, size * 0.75, size + 1);
            Assert.All(Painted(editor), p => Assert.InRange(p.X, 16 - size / 2 - 1, 48 + size / 2 + 1));
        }
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void TheOpacity_IsTheCeiling_EvenWhereAStrokeGoesOverItselfAgainAndAgain(string brush)
    {
        var editor = CreateEditor(CreateTool(brush, new StrokeSettings(10) { Opacity = 40 }));
        var scribble = Enumerable.Range(0, 12).SelectMany(i => Line(i % 2 == 0 ? (12, 32) : (52, 32), i % 2 == 0 ? (52, 32) : (12, 32), 20)).ToArray();

        TimedStroke(editor, TimeSpan.FromMilliseconds(50), scribble);

        // Red at 40% over white leaves green and blue at 255 − 0.4 × 255 = 153 or more.
        var painted = Painted(editor);
        Assert.NotEmpty(painted);
        Assert.All(painted, p => Assert.True(p.Color.G >= 152, $"{p} is darker than 40% red"));
        Assert.Contains(painted, p => p.Color.G <= 156);
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void TheSameStroke_AlwaysPaintsTheSamePixels(string brush)
    {
        Rgba[] Paint()
        {
            var editor = CreateEditor(CreateTool(brush, new StrokeSettings(16)));
            Script.Stroke(editor, PointerButton.Left, Line((10, 20), (40, 44), 15));
            return Snapshot(editor);
        }

        Assert.Equal(Paint(), Paint());
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void UndoingAStroke_RestoresTheLayer_AndRedoingRepaintsIt(string brush)
    {
        var editor = CreateEditor(CreateTool(brush, new StrokeSettings(20)));
        var blank = Snapshot(editor);
        Script.Stroke(editor, PointerButton.Left, Line((8, 8), (56, 56), 12));
        var painted = Snapshot(editor);

        editor.Undo();
        Assert.Equal(blank, Snapshot(editor));
        editor.Redo();
        Assert.Equal(painted, Snapshot(editor));
    }

    [Theory]
    [InlineData("oil")]
    [InlineData("watercolour")]
    public void AClick_LeavesOneDab(string brush)
    {
        var editor = CreateEditor(CreateTool(brush, new StrokeSettings(15)));

        Script.Stroke(editor, PointerButton.Left, (32.5, 32.5));

        Assert.NotEqual(Rgba.White, Pixels(editor).GetPixel(32, 32));
        Assert.All(Painted(editor), p => Assert.InRange(Math.Sqrt((p.X + 0.5 - 32.5) * (p.X + 0.5 - 32.5) + (p.Y + 0.5 - 32.5) * (p.Y + 0.5 - 32.5)), 0, 8));
    }

    [Fact]
    public void Airbrush_SpraysSinglePixelDotsOfTheFullColourWithinTheTip()
    {
        var editor = CreateEditor(new AirbrushTool(new StrokeSettings(20)));

        TimedStroke(editor, TimeSpan.FromMilliseconds(16), (32.5, 32.5), (32.5, 32.5), (32.5, 32.5));

        var painted = Painted(editor);
        Assert.InRange(painted.Count, 5, 400);
        Assert.All(painted, p => Assert.Equal(Red, p.Color));
        Assert.All(painted, p => Assert.InRange((p.X - 32) * (p.X - 32) + (p.Y - 32) * (p.Y - 32), 0, 11 * 11));
    }

    [Fact]
    public void Airbrush_KeepsSprayingWhileThePointerIsHeldStill()
    {
        var editor = CreateEditor(new AirbrushTool(new StrokeSettings(30)));
        var time = TimeSpan.Zero;
        editor.Apply(new PointerInput(PointerAction.Down, 32, 32, Time: time));
        var counts = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            time += TimeSpan.FromMilliseconds(100);
            editor.Apply(new PointerInput(PointerAction.Move, 32, 32, Time: time)); // as the shell repeats it
            counts.Add(Painted(editor).Count);
        }

        Assert.True(counts.Zip(counts.Skip(1)).All(c => c.Second > c.First), string.Join(", ", counts));
        editor.Apply(new PointerInput(PointerAction.Up, 32, 32, Time: time));
    }

    [Fact]
    public void Airbrush_SpraysByTime_SoAFastStrokeIsSparserThanASlowOne()
    {
        int Sprayed(TimeSpan interval)
        {
            var editor = CreateEditor(new AirbrushTool(new StrokeSettings(12)));
            TimedStroke(editor, interval, Line((10, 32), (54, 32), 11));
            return Painted(editor).Count;
        }

        Assert.True(Sprayed(TimeSpan.FromMilliseconds(4)) * 3 < Sprayed(TimeSpan.FromMilliseconds(40)));
    }

    [Fact]
    public void Airbrush_RepeatsAtTheNominalEventInterval() =>
        Assert.Equal(PointerInput.NominalInterval, new AirbrushTool(new StrokeSettings(5)).RepeatInterval);

    [Fact]
    public void Oil_DoesNotBuildUpWhereTheStrokeGoesBackOverItself()
    {
        // Only the ragged edge may differ: each pass has its own.
        IEnumerable<Rgba> Inside(params (double X, double Y)[][] legs)
        {
            var editor = CreateEditor(new OilBrushTool(new StrokeSettings(14)));
            Script.Stroke(editor, PointerButton.Left, [.. legs.SelectMany(l => l)]);
            return Enumerable.Range(28, 9).SelectMany(y => Enumerable.Range(20, 25).Select(x => Pixels(editor).GetPixel(x, y))).ToList();
        }

        var there = Line((12.5, 32.5), (52.5, 32.5), 20);
        var back = Line((52.5, 32.5), (12.5, 32.5), 20);

        Assert.Equal(Inside(there), Inside(there, back, there));
    }

    [Fact]
    public void Oil_IsMostlySolidPaint_WithLighterStreaks()
    {
        var editor = CreateEditor(new OilBrushTool(new StrokeSettings(24)), 128, 64);
        Script.Stroke(editor, PointerButton.Left, Line((16.5, 32.5), (112.5, 32.5), 48));

        var core = Enumerable.Range(24, 80).SelectMany(x => Enumerable.Range(26, 13).Select(y => Pixels(editor).GetPixel(x, y))).ToList();
        Assert.True(core.Count(c => c.G < 40) > core.Count * 0.6, "mostly solid");
        Assert.Contains(core, c => c.G > 40); // lighter streaks let the white through
    }

    [Fact]
    public void Watercolour_IsTranslucent_AndBuildsUpWhereTheStrokeGoesBackOverItself()
    {
        var editor = CreateEditor(new WatercolourBrushTool(new StrokeSettings(14)));

        Script.Stroke(editor, PointerButton.Left, [.. Line((8.5, 20.5), (56.5, 20.5), 24), .. Line((56.5, 20.5), (8.5, 20.5), 24)]);
        Script.Stroke(editor, PointerButton.Left, Line((8.5, 44.5), (56.5, 44.5), 24));

        var once = Pixels(editor).GetPixel(32, 44);
        var twice = Pixels(editor).GetPixel(32, 20);
        Assert.InRange(once.G, 100, 220); // a wash, not solid
        Assert.True(twice.G < once.G - 20, $"{twice} should be darker than {once}");
    }

    [Fact]
    public void Watercolour_DoesNotBuildUpAlongOneStraightPass_HoweverManyEventsItTakes()
    {
        Rgba[] Paint(int events)
        {
            var editor = CreateEditor(new WatercolourBrushTool(new StrokeSettings(14)));
            Script.Stroke(editor, PointerButton.Left, Line((8.5, 32.5), (56.5, 32.5), events));
            return Snapshot(editor);
        }

        var one = Paint(1);
        var many = Paint(48);
        Assert.All(one.Zip(many), p => Assert.InRange(Math.Abs(p.First.G - p.Second.G), 0, 1));
    }

    [Fact]
    public void PassStroke_KeepsTheHighestCoverageWithinAPass_AndBuildsUpAcrossPasses()
    {
        var layer = Document.CreateBlank(4, 1).ActiveLayer.Pixels;
        var stroke = new PassStroke(layer, Rgba.Black, 1f, newPassAfter: 10);

        stroke.Cover(0, 0, 1f, 0.5f, along: 0);
        stroke.Cover(0, 0, 0.5f, 0.5f, along: 5); // same pass, less coverage: no change
        Assert.Equal(127, layer.GetPixel(0, 0).G);

        stroke.Cover(0, 0, 1f, 0.5f, along: 11); // a new pass: 1 − 0.5 × 0.5 = 75%
        Assert.Equal(64, layer.GetPixel(0, 0).G);
        Assert.Equal(new IntRect(0, 0, 1, 1), stroke.TakeDirty());
    }

    [Fact]
    public void TexturedPath_PaintsAPolylineAndAnArea()
    {
        var layer = Document.CreateBlank(40, 40).ActiveLayer.Pixels;
        var tip = new OilTip(3, seed: 7);

        TexturedPath.Polyline(new PassStroke(layer, Rgba.Black, 1f, double.PositiveInfinity), tip, [(5, 5), (35, 5), (35, 15)]);
        TexturedPath.Area(new PassStroke(layer, Rgba.Black, 1f, double.PositiveInfinity), tip, new IntRect(5, 25, 30, 10), (_, _) => 1f);

        Assert.NotEqual(Rgba.White, layer.GetPixel(20, 5));
        Assert.NotEqual(Rgba.White, layer.GetPixel(35, 12));
        Assert.Equal(Rgba.White, layer.GetPixel(20, 15));
        Assert.All(Enumerable.Range(5, 30), x => Assert.NotEqual(Rgba.White, layer.GetPixel(x, 30)));
        Assert.Equal(Rgba.White, layer.GetPixel(20, 36));
    }
}
