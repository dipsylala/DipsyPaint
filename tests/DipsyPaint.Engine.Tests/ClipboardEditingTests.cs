using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class ClipboardEditingTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    /// <summary>A white 32×32 document with a red 4×4 square at (4, 4) and the selection tool active.</summary>
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

    private static PixelImage BlueImage(int width, int height) => new(width, height, Enumerable.Repeat(Blue, width * height).ToArray());

    [Fact]
    public void Copy_WithoutASelection_ReturnsNull()
    {
        var editor = CreateEditor();

        Assert.Null(editor.CopySelection());
    }

    [Fact]
    public void Copy_OfAMarquee_ReadsTheLayer_AndLeavesEverythingAsItWas()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectSquare(editor);

        var image = editor.CopySelection()!;

        Assert.Equal((6, 6), (image.Width, image.Height));
        Assert.Equal(Rgba.White, image.Pixels[0]);
        Assert.Equal(Red, image.Pixels[1 * 6 + 1]);
        Assert.Equal(new IntRect(3, 3, 6, 6), editor.Selection?.Bounds);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Copy_OfALiftedSelection_ReturnsThePixelsAsShown_AsACopy()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (7.5, 7.5)); // the red square exactly
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (8, 8), (12, 12));        // stretch it to 8×8

        var image = editor.CopySelection()!;

        Assert.Equal((8, 8), (image.Width, image.Height));
        Assert.All(image.Pixels, p => Assert.Equal(Red, p));
        Assert.NotSame(editor.Selection!.Value.FloatingPixels, image.Pixels);
    }

    [Fact]
    public void Cut_AfterAMove_IsOneHistoryStep()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectSquare(editor);
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5)); // move it by (10, 2)

        var image = editor.CopySelection()!;
        editor.DeleteSelection();

        Assert.Equal(Red, image.Pixels[1 * 6 + 1]);
        Assert.Null(editor.Selection);
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(14, 6));

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Paste_ShowsALiftedSelection_WithoutWritingTheLayer()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);

        editor.Paste(BlueImage(3, 2), 10, 20);

        Assert.Equal(new IntRect(10, 20, 3, 2), editor.Selection?.Bounds);
        Assert.NotNull(editor.Selection?.FloatingPixels);
        Assert.Equal(new Adornment(new IntRect(10, 20, 3, 2)), editor.Adornment);
        Assert.Equal(Blue, Shown(editor, 12, 21));
        Assert.Equal(before, Snapshot(editor)); // no hole, nothing written yet
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void PastedSelection_CommitsAsOneStep_ThatUndoes()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        editor.Paste(BlueImage(6, 6), 10, 20);
        Script.Stroke(editor, PointerButton.Left, (13, 23), (3, 3)); // move it to (0, 0)

        editor.CommitPending();

        Assert.Null(editor.Selection);
        Assert.Equal(Blue, Layer(editor).GetPixel(2, 1));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20)); // where it was pasted: untouched

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void CancellingAPaste_WritesNothing()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        editor.Paste(BlueImage(3, 2), 10, 20);

        editor.CancelPending();

        Assert.Null(editor.Selection);
        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void UndoRightAfterPasting_RemovesThePaste()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        editor.Paste(BlueImage(3, 2), 10, 20);

        editor.Undo();

        Assert.Null(editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
    }

    [Fact]
    public void Paste_SwitchesToTheSelectionTool_CommittingPendingContentFirst()
    {
        var editor = CreateEditor();
        editor.ActiveTool = new ShapeTool(new ShapeSettings());
        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), (28.5, 28.5)); // a pending shape
        var toolChanges = 0;
        editor.ActiveToolChanged += (_, _) => toolChanges++;

        editor.Paste(BlueImage(3, 2), 0, 0);

        Assert.IsType<SelectionTool>(editor.ActiveTool);
        Assert.Equal(1, toolChanges);
        Assert.True(editor.History.CanUndo); // the shape was committed
        Assert.Equal(new IntRect(0, 0, 3, 2), editor.Selection?.Bounds);
    }

    [Fact]
    public void PasteOverASelection_CommitsTheSelectionFirst()
    {
        var editor = CreateEditor();
        SelectSquare(editor);
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (15.5, 7.5)); // move the square

        editor.Paste(BlueImage(3, 2), 0, 0);

        Assert.Equal(Red, Layer(editor).GetPixel(14, 6));
        Assert.Equal(new IntRect(0, 0, 3, 2), editor.Selection?.Bounds);
    }

    [Fact]
    public void AnImageReachingPastTheCanvas_EnlargesIt_AsItsOwnStep()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);

        editor.Paste(BlueImage(30, 36), 10, 0);

        Assert.Equal((40, 36), (editor.Document.Width, editor.Document.Height));
        Assert.Equal(new IntRect(10, 0, 30, 36), editor.Selection?.Bounds);
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(39, 35)); // the new area, under the pasted pixels
        Assert.Equal(Red, Layer(editor).GetPixel(4, 4));

        editor.CommitPending();

        Assert.Equal(Blue, Layer(editor).GetPixel(39, 35));

        editor.Undo(); // the paste
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(39, 35));

        editor.Undo(); // the enlargement
        Assert.Equal((32, 32), (editor.Document.Width, editor.Document.Height));
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void AnImageTooLargeForTheMemoryBudgetToEnlargeFor_PastesWithoutEnlarging()
    {
        var editor = CreateEditor();

        editor.Paste(BlueImage(40, 36), 0, 0, new MemoryBudget(1024));

        Assert.Equal((32, 32), (editor.Document.Width, editor.Document.Height));
        Assert.Equal(new IntRect(0, 0, 40, 36), editor.Selection?.Bounds);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MovingAPastePastTheEdge_RedrawsOnlyWithinTheCanvas()
    {
        var editor = CreateEditor();
        editor.Paste(BlueImage(6, 6), 0, 0);
        var changed = new List<IntRect>();
        editor.CompositeChanged += (_, dirty) => changed.Add(dirty);

        Script.Stroke(editor, PointerButton.Left, (3, 3), (32, 32)); // to (29, 29), reaching past the edge

        Assert.Equal(new IntRect(29, 29, 6, 6), editor.Selection?.Bounds);
        Assert.NotEmpty(changed);
        Assert.All(changed, dirty => Assert.Equal(dirty, dirty.Intersect(new IntRect(0, 0, 32, 32))));
    }

    [Fact]
    public void SelectionChanged_IsRaisedWhenASelectionAppearsAndGoes()
    {
        var editor = CreateEditor();
        var changes = 0;
        editor.SelectionChanged += (_, _) => changes++;

        SelectSquare(editor);
        var afterSelecting = changes;
        editor.CommitPending(); // a marquee that was never lifted: nothing is redrawn

        Assert.True(afterSelecting > 0);
        Assert.Equal(afterSelecting + 1, changes);
        Assert.Null(editor.Selection);
    }
}
