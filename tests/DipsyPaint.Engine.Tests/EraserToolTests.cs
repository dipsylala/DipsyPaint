using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class EraserToolTests
{
    private static DocumentEditor CreateEditor(int size, int width = 32, int height = 32) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = new EraserTool(new StrokeSettings(size)) };

    private static List<(int X, int Y)> TransparentPixels(IPixelAccess pixels)
    {
        var found = new List<(int, int)>();
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                if (pixels.GetPixel(x, y) == Rgba.Transparent)
                {
                    found.Add((x, y));
                }
            }
        }

        return found;
    }

    [Theory]
    [InlineData(1, 10, 10)]
    [InlineData(4, 9, 9)]
    [InlineData(5, 8, 8)]
    public void Click_ErasesASquareToTransparency(int size, int left, int top)
    {
        var editor = CreateEditor(size);

        Script.Stroke(editor, PointerButton.Left, (10.5, 10.5));

        var expected = from y in Enumerable.Range(top, size) from x in Enumerable.Range(left, size) select (x, y);
        Assert.Equal(expected.OrderBy(p => p), TransparentPixels(editor.Document.ActiveLayer.Pixels).OrderBy(p => p));
    }

    [Fact]
    public void RightButton_AlsoErases()
    {
        var editor = CreateEditor(1);

        Script.Stroke(editor, PointerButton.Right, (3, 3));

        Assert.Equal([(3, 3)], TransparentPixels(editor.Document.ActiveLayer.Pixels));
    }

    [Fact]
    public void FastDiagonalMove_LeavesNoGaps()
    {
        var editor = CreateEditor(3);

        Script.Stroke(editor, PointerButton.Left, (2, 2), (29, 20));

        var erased = TransparentPixels(editor.Document.ActiveLayer.Pixels).ToHashSet();
        foreach (var (x, y) in Stroke.LinePoints(2, 2, 29, 20))
        {
            Assert.Contains((x, y), erased);
        }

        // The swept square stays a hard-edged band: nothing is partially erased.
        Assert.All(Enumerable.Range(0, 32 * 32), i =>
            Assert.Contains(editor.Document.ActiveLayer.Pixels.GetPixel(i % 32, i / 32), new[] { Rgba.White, Rgba.Transparent }));
    }

    [Fact]
    public void ErasingAnUpperLayer_RevealsTheLayerBelow()
    {
        var editor = CreateEditor(2);
        var top = editor.Document.AddLayer();
        top.Pixels.SetPixel(5, 5, Rgba.Black);
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));

        Script.Stroke(editor, PointerButton.Left, (5, 5));

        Assert.Equal(Rgba.Transparent, top.Pixels.GetPixel(5, 5));
        Assert.Equal(Rgba.White, editor.Compositor.Pixels[5 * 32 + 5]);
    }
}
