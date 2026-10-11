using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>Free-form selection, select all, invert selection and transparent selection (ticket 21).</summary>
public class SelectionModesTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    /// <summary>
    /// A white 32×32 document with a red 8×8 square at (4, 4), and a selection tool sharing
    /// <paramref name="settings"/> with the editor. Handles grab within 1 pixel.
    /// </summary>
    private static DocumentEditor CreateEditor(SelectionSettings? settings = null)
    {
        settings ??= new SelectionSettings();
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { SelectionSettings = settings, HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(settings);
        Layer(editor).WriteRegion(new IntRect(4, 4, 8, 8), Enumerable.Repeat(Red, 64).ToArray());
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

    /// <summary>
    /// Drags a free-form triangle around the red square's top-left half: (4, 4) → (11, 4) → (4, 11),
    /// closed back to (4, 4). Selects the pixels with x + y ≤ 15 within (4, 4)–(11, 11).
    /// </summary>
    private static void SelectTriangle(DocumentEditor editor)
    {
        editor.SelectionSettings.Shape = SelectionShape.FreeForm;
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (11.5, 4.5), (4.5, 11.5));
    }

    private static bool InTriangle(int x, int y) => x >= 4 && y >= 4 && x + y <= 15;

    [Fact]
    public void FreeForm_SelectsTheEnclosedPixels_FramedByTheirBounds_WithoutWriting()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);

        SelectTriangle(editor);

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(4, 4, 8, 8), selection.Bounds);
        Assert.Equal(new Adornment(new IntRect(4, 4, 8, 8)) { HasHandles = false }, editor.Adornment);
        Assert.NotNull(selection.Mask);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.Equal(InTriangle(x, y), selection.Contains(x, y));
            }
        }

        Assert.Null(selection.FloatingPixels);
        Assert.Equal(before, Snapshot(editor));
        editor.CommitPending();
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void FreeForm_ShowsItsPathWhileDragging_KeptWithinTheCanvas()
    {
        var editor = CreateEditor();
        editor.SelectionSettings.Shape = SelectionShape.FreeForm;
        var tool = (SelectionTool)editor.ActiveTool!;

        editor.Apply(new PointerInput(PointerAction.Down, 4.5, 4.5, PointerButton.Left));
        editor.Apply(new PointerInput(PointerAction.Move, 10.5, 4.5, PointerButton.Left));
        editor.Apply(new PointerInput(PointerAction.Move, 40.5, -3.5, PointerButton.Left));

        Assert.Equal(new[] { (4, 4), (10, 4), (31, 0) }, tool.FreeFormPath);
        Assert.Null(editor.Selection);

        editor.Apply(new PointerInput(PointerAction.Up, 40.5, -3.5, PointerButton.Left));

        Assert.Null(tool.FreeFormPath);
        Assert.Equal(new IntRect(4, 0, 28, 5), editor.Selection?.Bounds);
    }

    [Fact]
    public void FreeForm_AClickSelectsNothing()
    {
        var editor = CreateEditor();
        editor.SelectionSettings.Shape = SelectionShape.FreeForm;

        Script.Stroke(editor, PointerButton.Left, (6.5, 6.5), (6.5, 6.5));

        Assert.Null(editor.Selection);
        Assert.Null(editor.Adornment);
    }

    [Fact]
    public void FreeForm_MoveLiftsOnlyTheSelectedPixels_AsOneUndoableStep()
    {
        var editor = CreateEditor();
        var before = Snapshot(editor);
        SelectTriangle(editor);
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (21.5, 17.5)); // move by (16, 12)

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 11));
        Assert.Equal(Red, Layer(editor).GetPixel(11, 11)); // outside the triangle: stays
        Assert.Equal(Red, Shown(editor, 20, 16));
        Assert.Equal(Rgba.White, Shown(editor, 27, 23));   // the floating corner outside the triangle shows what is below
        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(20, 16, 8, 8), selection.Bounds);
        Assert.True(selection.Contains(20, 16));
        Assert.False(selection.Contains(27, 23));

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(20, 16));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(27, 23));

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void FreeForm_DeleteClearsOnlyTheSelectedPixels()
    {
        var editor = CreateEditor();
        SelectTriangle(editor);

        editor.DeleteSelection();

        for (var y = 4; y < 12; y++)
        {
            for (var x = 4; x < 12; x++)
            {
                Assert.Equal(InTriangle(x, y) ? Rgba.Transparent : Red, Layer(editor).GetPixel(x, y));
            }
        }

        Assert.Null(editor.Selection);
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void FreeForm_CopyIsTransparentOutsideTheShape_LiftedOrNot()
    {
        var editor = CreateEditor();
        SelectTriangle(editor);

        var copied = editor.CopySelection()!;
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (21.5, 17.5));
        var copiedAfterMoving = editor.CopySelection()!;

        foreach (var image in new[] { copied, copiedAfterMoving })
        {
            Assert.Equal((8, 8), (image.Width, image.Height));
            Assert.Equal(Red, image.Pixels[0]);
            Assert.Equal(Red, image.Pixels[7]);              // (11, 4): x + y = 15
            Assert.Equal(Rgba.Transparent, image.Pixels[63]); // (11, 11)
        }
    }

    [Fact]
    public void FreeForm_RotatingTurnsItsShapeToo()
    {
        var editor = CreateEditor();
        SelectTriangle(editor);

        editor.Transform(ImageTransform.FlipHorizontal);

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.True(selection.Contains(11, 4));
        Assert.True(selection.Contains(11, 11));
        Assert.False(selection.Contains(4, 11));
        Assert.Equal(Rgba.Transparent, selection.FloatingPixels![7 * 8 + 0]);
    }

    [Fact]
    public void SelectAll_SwitchesToTheSelectionTool_AndSelectsTheCanvas()
    {
        var editor = CreateEditor();
        var settings = editor.SelectionSettings;
        editor.ActiveTool = new PencilTool();
        var switched = 0;
        editor.ActiveToolChanged += (_, _) => switched++;

        editor.SelectAll();

        Assert.IsType<SelectionTool>(editor.ActiveTool);
        Assert.Equal(1, switched);
        Assert.Equal(new Selection(new IntRect(0, 0, 32, 32), null), editor.Selection);
        Assert.False(editor.History.CanUndo);

        settings.Shape = SelectionShape.FreeForm; // the editor's tool uses the shared settings
        editor.CommitPending();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (11.5, 4.5), (4.5, 11.5));
        Assert.NotNull(editor.Selection?.Mask);
    }

    [Fact]
    public void SelectAll_CommitsAMovedSelectionFirst()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (11.5, 11.5));
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (6.5, 6.5), (16.5, 6.5)); // move by (10, 0)

        editor.SelectAll();

        Assert.Equal(Red, Layer(editor).GetPixel(20, 4));
        Assert.True(editor.History.CanUndo);
        Assert.Equal(new IntRect(0, 0, 32, 32), editor.Selection?.Bounds);
    }

    [Fact]
    public void Invert_SelectsEverythingElse_AndDeleteClearsIt()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (11.5, 11.5)); // the red square

        editor.InvertSelection();

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(0, 0, 32, 32), selection.Bounds);
        Assert.False(selection.Contains(4, 4));
        Assert.True(selection.Contains(3, 4));
        Assert.True(selection.Contains(31, 31));

        editor.DeleteSelection();

        Assert.Equal(Red, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(3, 4));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(12, 12));
    }

    [Fact]
    public void Invert_OfAFreeFormSelection_IsTheRestOfTheCanvas_TrimmedToIt()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (31.5, 15.5)); // the top half
        editor.InvertSelection();
        Assert.Equal(new Selection(new IntRect(0, 16, 32, 16), null), editor.Selection);

        editor.CommitPending();
        SelectTriangle(editor);
        editor.InvertSelection();

        var selection = Assert.IsType<Selection>(editor.Selection);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.Equal(!InTriangle(x, y), selection.Contains(x, y));
            }
        }
    }

    [Fact]
    public void Invert_WithoutASelection_SelectsAll_AndOfAllSelectsNothing()
    {
        var editor = CreateEditor();

        editor.InvertSelection();
        Assert.Equal(new IntRect(0, 0, 32, 32), editor.Selection?.Bounds);

        editor.InvertSelection();
        Assert.Null(editor.Selection);
    }

    [Fact]
    public void Invert_AfterAMove_CommitsIt_AndInvertsWhereItNowIs()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (11.5, 11.5));
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (6.5, 6.5), (16.5, 6.5)); // move by (10, 0)

        editor.InvertSelection();

        Assert.Equal(Red, Layer(editor).GetPixel(20, 4));
        Assert.True(editor.History.CanUndo);
        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.False(selection.Contains(14, 4));
        Assert.True(selection.Contains(4, 4));
    }

    [Fact]
    public void Transparent_LeavesSecondaryColourPixelsBehind_AndShowsWhatIsBelowThem()
    {
        var editor = CreateEditor(new SelectionSettings { Transparent = true }); // colour 2 is white
        Layer(editor).WriteRegion(new IntRect(20, 4, 8, 8), Enumerable.Repeat(Blue, 64).ToArray());
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (13.5, 13.5)); // the red square and a white border
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (22.5, 5.5));   // over the blue square, by (17, 0)

        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 2));        // white was not lifted
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));  // red was
        Assert.Equal(Blue, Shown(editor, 20, 4));                      // the white border is see-through
        Assert.Equal(Red, Shown(editor, 21, 4));

        editor.CommitPending();

        Assert.Equal(Blue, Layer(editor).GetPixel(20, 4));
        Assert.Equal(Red, Layer(editor).GetPixel(28, 11));
    }

    [Fact]
    public void Transparent_AppliesToPastedPixels_AndFollowsTheSetting()
    {
        var settings = new SelectionSettings { Transparent = true };
        var editor = CreateEditor(settings);
        var image = new PixelImage(2, 1, [Rgba.White, Blue]);

        editor.Paste(image, 4, 4);

        Assert.Equal(Red, Shown(editor, 4, 4));
        Assert.Equal(Blue, Shown(editor, 5, 4));
        Assert.Equal(Rgba.White, image.Pixels[0]); // the pasted pixels are not changed

        settings.Transparent = false;
        editor.RefreshPending();

        Assert.Equal(Rgba.White, Shown(editor, 4, 4));
    }

    [Fact]
    public void Transparent_CopyLeavesOutTheSecondaryColour()
    {
        var editor = CreateEditor(new SelectionSettings { Transparent = true });
        Script.Stroke(editor, PointerButton.Left, (3.5, 3.5), (12.5, 12.5));

        var image = editor.CopySelection()!;

        Assert.Equal(Rgba.Transparent, image.Pixels[0]);
        Assert.Equal(Red, image.Pixels[1 * 10 + 1]);
    }

    [Fact]
    public void Select_TakesAnyMask_AndRejectsAWrongSize()
    {
        var editor = CreateEditor();
        bool[] mask = [true, false, false, true];

        editor.Select(new IntRect(4, 4, 2, 2), mask);

        Assert.Equal(new Selection(new IntRect(4, 4, 2, 2), null, mask), editor.Selection);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(new IntRect(4, 4, 3, 2), mask));
    }

    [Fact]
    public void FromPath_OfARectangle_HasNoMask()
    {
        var selection = SelectionMasks.FromPath([(2, 3), (6, 3), (6, 5), (2, 5)]);

        Assert.Equal(new Selection(new IntRect(2, 3, 5, 3), null), selection);
    }

    [Fact]
    public void Trim_ShrinksToTheSetPixels()
    {
        var selection = SelectionMasks.Trim(new IntRect(10, 10, 3, 3), [false, false, false, false, true, false, false, true, true]);

        Assert.Equal(new IntRect(11, 11, 2, 2), selection?.Bounds);
        Assert.Equal(new[] { true, false, true, true }, selection?.Mask);
        Assert.Null(SelectionMasks.Trim(new IntRect(0, 0, 2, 1), [false, false]));
    }
}
