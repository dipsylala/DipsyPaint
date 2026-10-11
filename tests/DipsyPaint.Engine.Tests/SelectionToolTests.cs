using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class SelectionToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>
    /// A white 32×32 document with a red 4×4 square at (4, 4). Handles grab within 1 pixel, as when
    /// zoomed in, so the small test selections can be moved from inside.
    /// </summary>
    private static DocumentEditor CreateEditor()
    {
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { ActiveTool = new SelectionTool(), HandleTolerance = 1 };
        Layer(editor).WriteRegion(new IntRect(4, 4, 4, 4), Enumerable.Repeat(Red, 16).ToArray());
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));
        return editor;
    }

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba Shown(DocumentEditor editor, int x, int y) => editor.Compositor.Pixels[y * editor.Compositor.Width + x];

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = new Rgba[32 * 32];
        Layer(editor).ReadRegion(new IntRect(0, 0, 32, 32), pixels);
        return pixels;
    }

    /// <summary>Selects the red square plus a one-pixel white border: pixels (3, 3) to (8, 8).</summary>
    private static void SelectSquare(DocumentEditor editor) => Script.Stroke(editor, PointerButton.Left, (3.5, 3.5), (8.5, 8.5));

    /// <summary>Selects the square, then switches to Move selected pixels (ADR-0025).</summary>
    private static void SelectSquareToMove(DocumentEditor editor)
    {
        SelectSquare(editor);
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
    }

    [Fact]
    public void Marquee_SelectsWithoutWriting_AndCommittingRecordsNoStep()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);

        SelectSquare(editor);

        Assert.Equal(new Selection(new IntRect(3, 3, 6, 6), null), editor.Selection);
        Assert.Equal(new Adornment(new IntRect(3, 3, 6, 6)) { HasHandles = false }, editor.Adornment);

        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), (20.5, 20.5)); // a click outside deselects

        Assert.Null(editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Marquee_StaysWithinTheCanvas()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (28.5, 28.5), (40.5, -5.5));

        Assert.Equal(new IntRect(28, 0, 4, 29), editor.Selection?.Bounds);
    }

    [Fact]
    public void ClickInsideWithoutMoving_LiftsNothing()
    {
        var editor = CreateEditor();
        SelectSquareToMove(editor);

        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (5.5, 5.5));
        editor.CommitPending();

        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Move_LiftsThePixels_LeavingTheLayerTransparent()
    {
        var editor = CreateEditor();
        SelectSquareToMove(editor);

        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Rgba.Transparent, Shown(editor, 4, 4));
        Assert.Equal(Red, Shown(editor, 14, 6));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(14, 6)); // floating, not yet on the layer
        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(13, 5, 6, 6), selection.Bounds);
        Assert.Equal(Red, selection.FloatingPixels![1 * 6 + 1]);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MovesUntilCommitted_AreOneStep_ThatUndoesAndRedoes()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectSquareToMove(editor);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));
        Script.Stroke(editor, PointerButton.Left, (15.5, 7.5), (20.5, 17.5)); // move again

        editor.CommitPending();

        Assert.Null(editor.Selection);
        Assert.Equal(Red, Layer(editor).GetPixel(19, 16));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(18, 15)); // the lifted white border came along
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        var after = Snapshot(editor);

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);

        editor.Redo();

        Assert.Equal(after, Snapshot(editor));
    }

    [Fact]
    public void MovedPixels_CompositeOverWhatIsBelow()
    {
        var editor = CreateEditor();
        var blue = new Rgba(0, 0, 255, 255);
        Layer(editor).WriteRegion(new IntRect(20, 10, 4, 4), new Rgba[16]);
        Layer(editor).SetPixel(21, 11, blue);
        Script.Stroke(editor, PointerButton.Left, (20.5, 10.5), (23.5, 13.5)); // a transparent square with a blue pixel
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (21.5, 11.5), (5.5, 5.5));   // onto the red square

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(4, 4));
        Assert.Equal(blue, Layer(editor).GetPixel(5, 5));
    }

    [Fact]
    public void SelectingAfterMoving_CommitsAndStartsANewMarquee()
    {
        var editor = CreateEditor();
        SelectSquareToMove(editor);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));
        Script.Use(editor, SelectionToolMode.Select);

        Script.Stroke(editor, PointerButton.Left, (25.5, 25.5), (29.5, 29.5));

        Assert.Equal(Red, Layer(editor).GetPixel(14, 6));
        Assert.True(editor.History.CanUndo);
        Assert.Equal(new Selection(new IntRect(25, 25, 5, 5), null), editor.Selection);
    }

    [Fact]
    public void SwitchingTools_CommitsTheMove()
    {
        var editor = CreateEditor();
        SelectSquareToMove(editor);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));

        editor.ActiveTool = new PencilTool();

        Assert.Equal(Red, Layer(editor).GetPixel(14, 6));
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void Cancel_PutsTheMovedPixelsBack()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectSquareToMove(editor);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));

        editor.CancelPending();

        Assert.Equal(new Selection(new IntRect(3, 3, 6, 6), null), editor.Selection); // back where it was lifted from
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(Red, Shown(editor, 4, 4));
        Assert.Equal(Rgba.White, Shown(editor, 14, 6));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Delete_ClearsTheSelectedPixels_AsOneUndoableStep()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectSquare(editor);

        editor.DeleteSelection();

        Assert.Null(editor.Selection);
        Assert.Null(editor.Adornment);
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(3, 3));
        Assert.Equal(Rgba.Transparent, Shown(editor, 8, 8));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(9, 9));

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DeleteAfterMoving_DiscardsTheFloatingPixels()
    {
        var editor = CreateEditor();
        SelectSquareToMove(editor);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5));

        editor.DeleteSelection();

        Assert.True(editor.Overlay.IsEmpty);
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(14, 6));

        editor.Undo();

        Assert.Equal(Red, Layer(editor).GetPixel(4, 4));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DraggingAHandle_StretchesThePixels()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (7.5, 7.5)); // the red square exactly
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Script.Stroke(editor, PointerButton.Left, (8, 8), (12, 12));        // bottom-right handle

        Assert.Equal(new IntRect(4, 4, 8, 8), editor.Selection?.Bounds);

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(11, 11));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(12, 12));
    }

    [Fact]
    public void RightButton_DoesNothing()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);

        Script.Stroke(editor, PointerButton.Right, (3.5, 3.5), (8.5, 8.5));

        Assert.Null(editor.Selection);
        Assert.Equal(before, Snapshot(editor));
    }
}
