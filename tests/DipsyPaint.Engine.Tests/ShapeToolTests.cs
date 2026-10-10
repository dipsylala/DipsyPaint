using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class ShapeToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static DocumentEditor CreateEditor(ShapeSettings settings, int width = 32, int height = 32) =>
        new(Document.CreateBlank(width, height))
        {
            ActiveTool = new ShapeTool(settings),
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };

    private static DocumentEditor CreateEditor(ShapeKind kind = ShapeKind.Rectangle) =>
        CreateEditor(new ShapeSettings { Kind = kind });

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba Shown(DocumentEditor editor, int x, int y) => editor.Compositor.Pixels[y * editor.Compositor.Width + x];

    private static List<(int X, int Y)> LayerPixelsOf(DocumentEditor editor, Rgba color)
    {
        var pixels = Layer(editor);
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
    public void DrawnShape_IsShownButPendingUntilCommitted()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (2.5, 3.5), (9.5, 7.5));

        Assert.Equal(Red, Shown(editor, 2, 3));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 3));
        Assert.False(editor.History.CanUndo);
        Assert.Equal(new Adornment(new IntRect(2, 3, 8, 5)), editor.Adornment);

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(2, 3));
        Assert.Null(editor.Adornment);
        Assert.True(editor.Overlay.IsEmpty);
    }

    [Fact]
    public void Rectangle_HasA1PxOutlineInThePrimaryColourAndNoFillByDefault()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (2, 3), (6, 6));
        editor.CommitPending();

        var outline = LayerPixelsOf(editor, Red);
        Assert.Equal(14, outline.Count); // 5×4 frame minus its 3×2 inside
        Assert.Contains((2, 3), outline);
        Assert.Contains((6, 6), outline);
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(4, 4));
    }

    [Fact]
    public void Fill_UsesTheSecondaryColour_AndRightButtonSwapsTheColours()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Rectangle, Fill = ShapeStyle.Solid };
        var editor = CreateEditor(settings);

        Script.Stroke(editor, PointerButton.Left, (0, 0), (4, 4));
        Script.Stroke(editor, PointerButton.Right, (10, 10), (14, 14));
        editor.CommitPending();

        Assert.Equal((Red, Blue), (Layer(editor).GetPixel(0, 0), Layer(editor).GetPixel(2, 2)));
        Assert.Equal((Blue, Red), (Layer(editor).GetPixel(10, 10), Layer(editor).GetPixel(12, 12)));
    }

    [Fact]
    public void NoOutline_FillsTheWholeShape()
    {
        var editor = CreateEditor(new ShapeSettings { Outline = ShapeStyle.None, Fill = ShapeStyle.Solid });

        Script.Stroke(editor, PointerButton.Left, (0, 0), (3, 3));
        editor.CommitPending();

        Assert.Equal(16, LayerPixelsOf(editor, Blue).Count);
        Assert.Empty(LayerPixelsOf(editor, Red));
    }

    [Fact]
    public void Size_ThickensTheOutlineInwards()
    {
        var editor = CreateEditor(new ShapeSettings { Size = 3 });

        Script.Stroke(editor, PointerButton.Left, (0, 0), (9, 9));
        editor.CommitPending();

        Assert.Equal(100 - 16, LayerPixelsOf(editor, Red).Count);
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(3, 3));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 10));
    }

    [Fact]
    public void Line_IsAnAliased1PxLineBetweenThePressAndReleasePixels()
    {
        var editor = CreateEditor(ShapeKind.Line);

        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (6.5, 2.5));
        editor.CommitPending();

        Assert.Equal([(0, 0), (1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2)], LayerPixelsOf(editor, Red).OrderBy(p => p.X));
        Assert.Equal(32 * 32 - 7, LayerPixelsOf(editor, Rgba.White).Count);
    }

    [Fact]
    public void Ellipse_TouchesEverySideOfItsFrame()
    {
        var editor = CreateEditor(ShapeKind.Ellipse);

        Script.Stroke(editor, PointerButton.Left, (4, 4), (20, 12));
        editor.CommitPending();

        var outline = LayerPixelsOf(editor, Red);
        Assert.Equal(4, outline.Min(p => p.X));
        Assert.Equal(20, outline.Max(p => p.X));
        Assert.Equal(4, outline.Min(p => p.Y));
        Assert.Equal(12, outline.Max(p => p.Y));
        Assert.DoesNotContain((4, 4), outline); // corners stay clear
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(12, 8));
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle, 10.5, 4.5, 10, 10)]  // square
    [InlineData(ShapeKind.Ellipse, 3.5, 10.5, 10, 10)]    // circle
    [InlineData(ShapeKind.Line, 10.5, 2.5, 10, 0)]        // horizontal
    [InlineData(ShapeKind.Line, 9.5, 8.5, 9, 9)]          // 45°
    public void Shift_ConstrainsTheShape(ShapeKind kind, double x, double y, int expectedX, int expectedY)
    {
        var editor = CreateEditor(kind);

        editor.Apply(new PointerInput(PointerAction.Down, 0.5, 0.5));
        editor.Apply(new PointerInput(PointerAction.Move, x, y, Shift: true));
        editor.Apply(new PointerInput(PointerAction.Up, x, y, Shift: true));

        Assert.Equal(Adornment.FrameOf((0, 0), (expectedX, expectedY)), editor.Adornment!.Value.Frame);
    }

    [Fact]
    public void DraggingAHandle_ResizesThePendingShape()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 15.5)); // right handle at (10, 9)

        Script.Stroke(editor, PointerButton.Left, (10, 9), (15, 9));

        Assert.Equal(new IntRect(2, 2, 13, 14), editor.Adornment!.Value.Frame);
        Assert.Equal(Red, Shown(editor, 14, 2));
        Assert.Equal(Rgba.White, Shown(editor, 9, 5)); // the old right edge is gone
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DraggingInside_MovesThePendingShape()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (15.5, 15.5));

        Script.Stroke(editor, PointerButton.Left, (9.5, 9.5), (19.5, 11.5));

        Assert.Equal(new IntRect(12, 4, 14, 14), editor.Adornment!.Value.Frame);
        Assert.Equal(Rgba.White, Shown(editor, 2, 2));
        Assert.Equal(Red, Shown(editor, 12, 4));
    }

    [Fact]
    public void PressingOutside_CommitsAsOneStepAndStartsTheNextShape()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));

        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), (25.5, 25.5));

        Assert.Equal(Red, Layer(editor).GetPixel(2, 2));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(20, 20)); // the new shape is pending
        Assert.Equal(new IntRect(20, 20, 6, 6), editor.Adornment!.Value.Frame);

        editor.Undo(); // commits the second shape, then undoes it
        editor.Undo();

        Assert.Empty(LayerPixelsOf(editor, Red));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Commit_IsOneUndoableStep()
    {
        var editor = CreateEditor();
        var blank = editor.Compositor.Pixels.ToArray();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));
        Script.Stroke(editor, PointerButton.Left, (9.5, 9.5), (12.5, 12.5)); // resize first

        editor.CommitPending();
        var committed = editor.Compositor.Pixels.ToArray();
        Assert.True(editor.History.CanUndo);

        editor.Undo();
        Assert.Equal(blank, editor.Compositor.Pixels.ToArray());
        Assert.False(editor.History.CanUndo);

        editor.Redo();
        Assert.Equal(committed, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void UndoWhilePending_RemovesThePendingShape_AndRedoRestoresIt()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));

        editor.Undo();

        Assert.Equal(Rgba.White, Shown(editor, 2, 2));
        Assert.Null(editor.Adornment);

        editor.Redo();

        Assert.Equal(Red, Layer(editor).GetPixel(2, 2));
    }

    [Fact]
    public void SwitchingTools_CommitsThePendingShape()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));

        editor.ActiveTool = new PencilTool();

        Assert.Equal(Red, Layer(editor).GetPixel(2, 2));
        Assert.True(editor.History.CanUndo);
        Assert.True(editor.Overlay.IsEmpty);
    }

    [Fact]
    public void Cancel_DiscardsThePendingShapeWithoutAStep()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));

        editor.CancelPending();

        Assert.Equal(Rgba.White, Shown(editor, 2, 2));
        Assert.Null(editor.Adornment);
        Assert.False(editor.History.CanUndo);
        editor.CommitPending();
        Assert.Empty(LayerPixelsOf(editor, Red));
    }

    [Fact]
    public void Click_DrawsNoShape()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5));
        editor.CommitPending();

        Assert.Null(editor.Adornment);
        Assert.Empty(LayerPixelsOf(editor, Red));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void ChangingOptionsOrColours_UpdatesThePendingShape()
    {
        var settings = new ShapeSettings();
        var editor = CreateEditor(settings);
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5));

        settings.Fill = ShapeStyle.Solid;
        editor.RefreshPending();
        Assert.Equal(Blue, Shown(editor, 5, 5));

        editor.PrimaryColor = Rgba.Black;
        Assert.Equal(Rgba.Black, Shown(editor, 2, 2));
    }

    [Fact]
    public void HandleTolerance_FollowsTheEditor()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 29.5)); // right handle at (10, 16)
        editor.HandleTolerance = 8;

        Script.Stroke(editor, PointerButton.Left, (17, 16), (20, 16)); // 7 px right of the handle still grabs it

        Assert.Equal(new IntRect(2, 2, 11, 28), editor.Adornment!.Value.Frame);
    }

    [Fact]
    public void ShapeOffTheCanvas_IsClippedWhenCommitted()
    {
        var editor = CreateEditor(new ShapeSettings(), width: 8, height: 8);

        Script.Stroke(editor, PointerButton.Left, (-4.5, -4.5), (20.5, 3.5));
        editor.CommitPending();

        Assert.Equal(8, LayerPixelsOf(editor, Red).Count); // only the bottom edge, at y = 3, is on the canvas
    }
}
