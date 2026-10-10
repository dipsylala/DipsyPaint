using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class PencilToolTests
{
    private static DocumentEditor CreateEditor(int width = 16, int height = 16) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = new PencilTool() };

    private static IPixelAccess Pixels(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static List<(int X, int Y)> PixelsOf(DocumentEditor editor, Rgba color)
    {
        var pixels = Pixels(editor);
        var found = new List<(int, int)>();
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                if (pixels.GetPixel(x, y) == color)
                {
                    found.Add((x, y));
                }
            }
        }

        return found;
    }

    [Fact]
    public void Click_PaintsThePixelUnderThePointerInPrimaryColor()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (3.7, 5.2));

        Assert.Equal([(3, 5)], PixelsOf(editor, Rgba.Black));
    }

    [Fact]
    public void RightButton_PaintsInSecondaryColor()
    {
        var editor = CreateEditor();
        editor.SecondaryColor = new Rgba(0, 0, 255, 255);

        Script.Stroke(editor, PointerButton.Right, (1, 1));

        Assert.Equal(new Rgba(0, 0, 255, 255), Pixels(editor).GetPixel(1, 1));
    }

    [Fact]
    public void Drag_DrawsAnAliasedOnePixelLine()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (0, 0), (6, 2));

        Assert.Equal([(0, 0), (1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2)], PixelsOf(editor, Rgba.Black).OrderBy(p => p.X));
        Assert.Equal(16 * 16 - 7, PixelsOf(editor, Rgba.White).Count); // hard-edged: no partial pixels
    }

    [Fact]
    public void Drag_JoinsSuccessiveMovesWithoutGaps()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (1, 1), (10, 1), (10, 8));

        var drawn = PixelsOf(editor, Rgba.Black);
        Assert.Equal(10 + 7, drawn.Count);
        Assert.Contains((10, 8), drawn);
    }

    [Fact]
    public void StrokeLeavingTheCanvas_IsClipped()
    {
        var editor = CreateEditor(8, 8);

        Script.Stroke(editor, PointerButton.Left, (-4, 2), (20, 2));

        Assert.Equal(8, PixelsOf(editor, Rgba.Black).Count);
    }

    [Fact]
    public void Handle_ReportsTheChangedRectangle()
    {
        var document = Document.CreateBlank(16, 16);
        var context = new ToolContext(document.ActiveLayer.Pixels, Rgba.Black, Rgba.White);
        var pencil = new PencilTool();

        var down = pencil.Handle(context, new PointerInput(PointerAction.Down, 2, 3));
        var move = pencil.Handle(context, new PointerInput(PointerAction.Move, 6, 5));
        var up = pencil.Handle(context, new PointerInput(PointerAction.Up, 6, 5));
        var stray = pencil.Handle(context, new PointerInput(PointerAction.Move, 9, 9));

        Assert.Equal(new IntRect(2, 3, 1, 1), down);
        Assert.Equal(new IntRect(3, 4, 4, 2), move);
        Assert.True(up.IsEmpty);
        Assert.True(stray.IsEmpty);
    }

    [Fact]
    public void DrawsOnTheActiveLayerOnly()
    {
        var editor = CreateEditor();
        var top = editor.Document.AddLayer();

        Script.Stroke(editor, PointerButton.Left, (4, 4));

        Assert.Equal(Rgba.Black, top.Pixels.GetPixel(4, 4));
        Assert.Equal(Rgba.White, editor.Document.Layers[0].Pixels.GetPixel(4, 4));
    }
}
