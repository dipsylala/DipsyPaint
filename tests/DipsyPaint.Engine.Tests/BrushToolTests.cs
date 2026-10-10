using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class BrushToolTests
{
    private static DocumentEditor CreateEditor(StrokeSettings settings, int width = 32, int height = 32) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = new BrushTool(settings) };

    private static IPixelAccess Pixels(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    /// <summary>Columns of row <paramref name="y"/> that are not white.</summary>
    private static List<int> PaintedColumns(DocumentEditor editor, int y) =>
        [.. Enumerable.Range(0, Pixels(editor).Width).Where(x => Pixels(editor).GetPixel(x, y) != Rgba.White)];

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    public void Click_PaintsARoundDabOfTheSetDiameter(int size)
    {
        var editor = CreateEditor(new StrokeSettings(size));

        Script.Stroke(editor, PointerButton.Left, (16.5, 16.5));

        var opaque = Enumerable.Range(0, 32).Count(x => Pixels(editor).GetPixel(x, 16) == Rgba.Black);
        Assert.InRange(opaque, size - 1, size + 1);
        Assert.Equal(Rgba.Black, Pixels(editor).GetPixel(16, 16));
        if (size >= 5)
        {
            var corner = 16 - size / 2;
            Assert.True(Pixels(editor).GetPixel(corner, corner).R > 128); // round, not square: corners mostly unpainted
        }
    }

    [Fact]
    public void Edges_AreAntialiased()
    {
        var editor = CreateEditor(new StrokeSettings(9));

        Script.Stroke(editor, PointerButton.Left, (16, 16));

        var partial = Enumerable.Range(0, 32 * 32)
            .Select(i => Pixels(editor).GetPixel(i % 32, i / 32))
            .Count(p => p != Rgba.Black && p != Rgba.White);
        Assert.True(partial > 0);
    }

    [Fact]
    public void RightButton_PaintsInSecondaryColor()
    {
        var editor = CreateEditor(new StrokeSettings(3));
        editor.SecondaryColor = new Rgba(0, 0, 255, 255);

        Script.Stroke(editor, PointerButton.Right, (5, 5));

        Assert.Equal(new Rgba(0, 0, 255, 255), Pixels(editor).GetPixel(5, 5));
    }

    [Fact]
    public void FastMove_LeavesNoGaps()
    {
        var editor = CreateEditor(new StrokeSettings(3), width: 64);

        Script.Stroke(editor, PointerButton.Left, (2, 8), (61, 8));

        Assert.Equal(Enumerable.Range(1, 62), PaintedColumns(editor, 8));
    }

    [Fact]
    public void CrossingItself_DoesNotBuildUpPartialPixels()
    {
        var once = CreateEditor(new StrokeSettings(6));
        Script.Stroke(once, PointerButton.Left, (4, 10), (28, 10));
        var twice = CreateEditor(new StrokeSettings(6));
        Script.Stroke(twice, PointerButton.Left, (4, 10), (28, 10), (4, 10));

        for (var x = 0; x < 32; x++)
        {
            for (var y = 0; y < 32; y++)
            {
                Assert.Equal(Pixels(once).GetPixel(x, y), Pixels(twice).GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void SizeChange_AppliesToTheNextStroke()
    {
        var settings = new StrokeSettings(3);
        var editor = CreateEditor(settings);
        Script.Stroke(editor, PointerButton.Left, (16, 4));

        settings.Size = 11;
        Script.Stroke(editor, PointerButton.Left, (16, 20));

        Assert.InRange(PaintedColumns(editor, 4).Count, 3, 5);
        Assert.InRange(PaintedColumns(editor, 20).Count, 11, 13);
    }

    [Fact]
    public void Size_IsClampedToTheSupportedRange()
    {
        Assert.Equal(StrokeSettings.MinSize, new StrokeSettings(0).Size);
        Assert.Equal(StrokeSettings.MaxSize, new StrokeSettings(500).Size);
    }

    [Fact]
    public void StrokeLeavingTheCanvas_IsClipped()
    {
        var editor = CreateEditor(new StrokeSettings(4), width: 8, height: 8);

        Script.Stroke(editor, PointerButton.Left, (-40, 4), (40, 4));

        Assert.Equal(Enumerable.Range(0, 8), PaintedColumns(editor, 4));
    }
}
