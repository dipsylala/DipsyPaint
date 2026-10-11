using DipsyPaint.Engine.Tests.Golden;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests;

public class ArtisticBrushToolTests
{
    public static TheoryData<string> Brushes => ArtisticBrushGoldenTests.Brushes;

    private static DocumentEditor CreateEditor(string brush, StrokeSettings settings, int width = 48, int height = 48) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = new ArtisticBrushTool(settings, ArtisticBrushGoldenTests.TipOf(brush)) };

    private static IPixelAccess Pixels(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba[] AllPixels(DocumentEditor editor)
    {
        var pixels = Pixels(editor);
        var all = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), all);
        return all;
    }

    /// <summary>How many pixels of column <paramref name="x"/> are painted.</summary>
    private static int PaintedInColumn(DocumentEditor editor, int x) =>
        Enumerable.Range(0, Pixels(editor).Height).Count(y => Pixels(editor).GetPixel(x, y) != Rgba.White);

    [Theory]
    [MemberData(nameof(Brushes))]
    public void Stroke_PaintsAlongThePath_WithinTheSetSize(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(12));

        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));

        Assert.InRange(PaintedInColumn(editor, 24), 2, 13);
        for (var x = 0; x < 48; x++)
        {
            for (var y = 0; y < 48; y++)
            {
                if (Math.Abs(y + 0.5 - 24.5) > 7.5 || x < 1 || x > 47)
                {
                    Assert.Equal(Rgba.White, Pixels(editor).GetPixel(x, y));
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void Size_ScalesTheMark(string brush)
    {
        var small = CreateEditor(brush, new StrokeSettings(4));
        Script.Stroke(small, PointerButton.Left, (8, 24), (40, 24));
        var large = CreateEditor(brush, new StrokeSettings(24));
        Script.Stroke(large, PointerButton.Left, (8, 24), (40, 24));

        Assert.True(PaintedInColumn(large, 24) > 3 * PaintedInColumn(small, 24));
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void Opacity_CapsHowMuchAnyPixelIsCovered(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(16) { Opacity = 40 });

        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));

        // Black at 40 % over white is 153 at most; anything darker would exceed the opacity.
        Assert.All(AllPixels(editor), p => Assert.True(p.R >= 152, $"{p} is darker than 40 % black"));
        Assert.Contains(AllPixels(editor), p => p.R < 200);
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void RightButton_PaintsInTheSecondaryColour(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(16));
        editor.SecondaryColor = new Rgba(0, 0, 255, 255);

        Script.Stroke(editor, PointerButton.Right, (8, 24), (40, 24));

        var painted = AllPixels(editor).Where(p => p != Rgba.White).ToList();
        Assert.NotEmpty(painted);
        Assert.All(painted, p => Assert.True(p.B == 255 && p.R == p.G && p.R < 255, $"{p} is not a tint of blue"));
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void CrossingItself_DoesNotBuildUp(string brush)
    {
        var once = CreateEditor(brush, new StrokeSettings(10));
        Script.Stroke(once, PointerButton.Left, (8, 24), (40, 24));
        var twice = CreateEditor(brush, new StrokeSettings(10));
        Script.Stroke(twice, PointerButton.Left, (8, 24), (40, 24), (8, 24), (40, 24));

        Assert.Equal(AllPixels(once), AllPixels(twice));
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void SeparateStrokes_LayerUp(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(10) { Opacity = 50 });
        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));
        var first = AllPixels(editor);

        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));

        Assert.Contains(Enumerable.Range(0, first.Length), i => AllPixels(editor)[i].R < first[i].R);
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void UndoAndRedo_RestoreTheStrokeExactly(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(14) { Opacity = 70 });
        var blank = AllPixels(editor);
        Script.Stroke(editor, PointerButton.Left, (4, 4), (40, 30), (10, 44));
        var painted = AllPixels(editor);

        editor.Undo();
        Assert.Equal(blank, AllPixels(editor));
        editor.Redo();
        Assert.Equal(painted, AllPixels(editor));
    }

    [Theory]
    [MemberData(nameof(Brushes))]
    public void StrokesFarOffTheCanvas_PaintOnlyTheCanvas(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(20));

        Script.Stroke(editor, PointerButton.Left, (-5000, 24), (5000, 24), (5000, -5000));

        Assert.True(PaintedInColumn(editor, 24) > 0);
    }

    [Theory]
    [InlineData("calligraphy-brush", 1, -1)]
    [InlineData("calligraphy-pen", 1, 1)]
    public void Calligraphy_IsThinAlongTheNib_AndBroadAcrossIt(string brush, int nibX, int nibY)
    {
        static int Painted(DocumentEditor editor) => AllPixels(editor).Count(p => p != Rgba.White);

        var along = CreateEditor(brush, new StrokeSettings(18));
        Script.Stroke(along, PointerButton.Left, (24 - 10 * nibX, 24 - 10 * nibY), (24 + 10 * nibX, 24 + 10 * nibY));
        var across = CreateEditor(brush, new StrokeSettings(18));
        Script.Stroke(across, PointerButton.Left, (24 - 10 * nibY, 24 + 10 * nibX), (24 + 10 * nibY, 24 - 10 * nibX));

        Assert.True(Painted(across) > 2 * Painted(along), $"across {Painted(across)}, along {Painted(along)}");
    }

    [Theory]
    [InlineData("calligraphy-brush")]
    [InlineData("calligraphy-pen")]
    public void Calligraphy_IsSolidInk(string brush)
    {
        var editor = CreateEditor(brush, new StrokeSettings(18));

        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));

        Assert.Equal(Rgba.Black, Pixels(editor).GetPixel(24, 24));
    }

    [Fact]
    public void Marker_IsEvenTranslucentInk()
    {
        var editor = CreateEditor("marker", new StrokeSettings(16));

        Script.Stroke(editor, PointerButton.Left, (8, 24), (40, 24));

        // 60 % black over white.
        Assert.Equal(new Rgba(102, 102, 102, 255), Pixels(editor).GetPixel(24, 24));
        Assert.Equal(Pixels(editor).GetPixel(24, 24), Pixels(editor).GetPixel(30, 20));
    }

    [Theory]
    [InlineData("crayon")]
    [InlineData("natural-pencil")]
    public void TexturedBrushes_AreGrainy_AndTheGrainIsFixedToThePaper(string brush)
    {
        var first = CreateEditor(brush, new StrokeSettings(24));
        Script.Stroke(first, PointerButton.Left, (8, 24), (40, 24));
        var second = CreateEditor(brush, new StrokeSettings(24));
        Script.Stroke(second, PointerButton.Left, (40, 24), (30, 24), (8, 24));

        var centre = Enumerable.Range(14, 20).Select(x => Pixels(first).GetPixel(x, 24).R).ToList();
        Assert.True(centre.Distinct().Count() > 3, "the centre of the stroke has no grain");
        Assert.Equal(AllPixels(first), AllPixels(second));
    }

    [Fact]
    public void ATip_RendersAPolyline_LikeAStrokeThroughTheSamePoints()
    {
        var editor = CreateEditor("crayon", new StrokeSettings(10));
        Script.Stroke(editor, PointerButton.Left, (8.5, 8.5), (40.5, 20.5), (10.5, 40.5));
        var target = Document.CreateBlank(48, 48).ActiveLayer.Pixels;

        BrushTip.Crayon.Polyline(new Stroke(target, Rgba.Black), [(8.5, 8.5), (40.5, 20.5), (10.5, 40.5)], 10);

        var drawn = new Rgba[48 * 48];
        target.ReadRegion(new IntRect(0, 0, 48, 48), drawn);
        Assert.Equal(AllPixels(editor), drawn);
    }

    [Theory]
    [InlineData("crayon")]
    [InlineData("marker")]
    [InlineData("natural-pencil")]
    [InlineData("calligraphy-brush")]
    public void ATip_FillsAnAreaWithItsTexture_AsDeepInsideAStroke(string brush)
    {
        var tip = ArtisticBrushGoldenTests.TipOf(brush);
        var stroked = Document.CreateBlank(48, 48).ActiveLayer.Pixels;
        tip.Segment(new Stroke(stroked, Rgba.Black), 0, 24, 48, 24, 100);
        var filled = Document.CreateBlank(48, 48).ActiveLayer.Pixels;
        var fill = new Stroke(filled, Rgba.Black);

        for (var x = 20; x < 28; x++)
        {
            tip.Fill(fill, x, 24, 1f);
        }

        tip.Fill(fill, 30, 24, 0.5f);
        for (var x = 20; x < 28; x++)
        {
            // The middle of a 100 px stroke has (almost) the tip's full pressure, so it matches the fill.
            Assert.InRange(filled.GetPixel(x, 24).R - stroked.GetPixel(x, 24).R, -2, 2);
        }

        Assert.True(filled.GetPixel(30, 24).R >= 127);
    }
}
