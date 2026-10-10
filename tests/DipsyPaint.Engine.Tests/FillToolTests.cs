using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class FillToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static DocumentEditor CreateEditor(int width = 16, int height = 16) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = new FillTool() };

    private static IPixelAccess Pixels(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static int Count(IPixelAccess pixels, Rgba color)
    {
        var count = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                count += pixels.GetPixel(x, y) == color ? 1 : 0;
            }
        }

        return count;
    }

    private static void Pencil(DocumentEditor editor, params (double X, double Y)[] points)
    {
        var tool = editor.ActiveTool;
        editor.ActiveTool = new PencilTool();
        Script.Stroke(editor, PointerButton.Left, points);
        editor.ActiveTool = tool;
    }

    [Fact]
    public void Click_FillsTheWholeBlankCanvasWithThePrimaryColor()
    {
        var editor = CreateEditor();
        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (3.5, 7.5));

        Assert.Equal(16 * 16, Count(Pixels(editor), Red));
    }

    [Fact]
    public void RightButton_FillsWithTheSecondaryColor()
    {
        var editor = CreateEditor();
        editor.SecondaryColor = Blue;

        Script.Stroke(editor, PointerButton.Right, (3.5, 7.5));

        Assert.Equal(16 * 16, Count(Pixels(editor), Blue));
    }

    [Fact]
    public void Fill_StopsAtADiagonalLine_BecauseRegionsAre4Connected()
    {
        var editor = CreateEditor();
        Pencil(editor, (0, 15), (15, 0)); // anti-diagonal: 8-connected, no 4-connected gaps
        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(15 * 16 / 2, Count(Pixels(editor), Red)); // the triangle above the line
        Assert.Equal(Rgba.White, Pixels(editor).GetPixel(15, 15));
    }

    [Fact]
    public void Fill_MatchesTheClickedColorExactly()
    {
        var editor = CreateEditor();
        Pixels(editor).SetPixel(5, 5, new Rgba(254, 254, 254, 255));
        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (0, 0));

        Assert.Equal(16 * 16 - 1, Count(Pixels(editor), Red));
        Assert.Equal(new Rgba(254, 254, 254, 255), Pixels(editor).GetPixel(5, 5));
    }

    [Fact]
    public void Fill_ReplacesTransparentPixelsOnTheActiveLayerOnly()
    {
        var editor = CreateEditor();
        var background = editor.Document.Layers[0].Pixels;
        var top = editor.Document.AddLayer().Pixels;
        top.SetPixel(4, 4, Blue);
        editor.PrimaryColor = Red.WithOpacity(0.5f);

        Script.Stroke(editor, PointerButton.Left, (0, 0));

        Assert.Equal(16 * 16 - 1, Count(top, Red.WithOpacity(0.5f))); // replaced, not blended
        Assert.Equal(16 * 16, Count(background, Rgba.White));
        Assert.Equal(Red.WithOpacity(0.5f).Over(Rgba.White), editor.Compositor.Pixels[0]);
    }

    [Fact]
    public void ClickOffTheCanvasOrOnTheFillColor_ChangesNothingAndRecordsNoStep()
    {
        var editor = CreateEditor();
        editor.PrimaryColor = Rgba.White;

        Script.Stroke(editor, PointerButton.Left, (-1, 3));
        Script.Stroke(editor, PointerButton.Left, (3, 16));
        Script.Stroke(editor, PointerButton.Left, (3, 3));

        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DraggingAfterThePress_DoesNotFillAgain()
    {
        var editor = CreateEditor();
        Pencil(editor, (8, 0), (8, 15)); // splits the canvas in two
        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (2, 2), (12, 2));

        Assert.Equal(8 * 16, Count(Pixels(editor), Red));
        Assert.Equal(Rgba.White, Pixels(editor).GetPixel(12, 2));
    }

    [Fact]
    public void Fill_IsOneUndoableHistoryStep_SizedByTheFilledArea()
    {
        var editor = CreateEditor(512, 512);
        Pencil(editor, (10, 10), (40, 10), (40, 40), (10, 40), (10, 10));
        var before = editor.Compositor.Pixels.ToArray();
        var sizeBefore = editor.History.SizeBytes;
        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (20, 20));

        Assert.Equal(29 * 29, Count(Pixels(editor), Red));
        Assert.True(editor.History.SizeBytes - sizeBefore <= 64 * 64 * 4, "a fill inside one tile stores one tile");

        editor.Undo();

        Assert.Equal(before, editor.Compositor.Pixels.ToArray());
        editor.Undo();
        Assert.False(editor.History.CanUndo); // the outline was the only other step

        editor.Redo();
        editor.Redo();
        Assert.Equal(29 * 29, Count(Pixels(editor), Red));
    }

    [Fact]
    public void Fill_Of4KCanvas_DoesNotOverflowTheStack()
    {
        // A serpentine wall makes the region one long winding corridor: deep for a recursive fill.
        var editor = CreateEditor(3840, 2160);
        var pixels = Pixels(editor);
        for (var y = 1; y < pixels.Height; y += 2)
        {
            var gap = y % 4 == 1 ? pixels.Width - 1 : 0;
            for (var x = 0; x < pixels.Width; x++)
            {
                if (x != gap)
                {
                    pixels.SetPixel(x, y, Rgba.Black);
                }
            }
        }

        editor.PrimaryColor = Red;

        Script.Stroke(editor, PointerButton.Left, (0, 0));

        Assert.Equal(3840 * 1080 + 1080, Count(pixels, Red)); // every even row plus one gap per wall
    }
}
