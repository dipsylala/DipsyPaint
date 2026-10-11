using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>Select, Move selection and Move selected pixels: separate tools for making and moving selections (ADR-0025).</summary>
public class SelectionMoveToolsTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>
    /// A white 32×32 document with a red 4×4 square at (4, 4); Select is active, with the editor's selection
    /// options. Handles grab within 1 pixel.
    /// </summary>
    private static DocumentEditor CreateEditor()
    {
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(editor.SelectionSettings);
        Layer(editor).WriteRegion(new IntRect(4, 4, 4, 4), Enumerable.Repeat(Red, 16).ToArray());
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));
        return editor;
    }

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = new Rgba[32 * 32];
        Layer(editor).ReadRegion(new IntRect(0, 0, 32, 32), pixels);
        return pixels;
    }

    /// <summary>Selects the red square exactly, (4, 4) to (7, 7), then switches to <paramref name="mode"/>.</summary>
    private static DocumentEditor WithSquareSelected(SelectionToolMode mode)
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (7.5, 7.5));
        Script.Use(editor, mode);
        return editor;
    }

    private static void Drag(DocumentEditor editor, (double X, double Y) from, (double X, double Y) to, bool shift = false)
    {
        editor.Apply(new PointerInput(PointerAction.Down, from.X, from.Y));
        editor.Apply(new PointerInput(PointerAction.Move, to.X, to.Y, Shift: shift));
        editor.Apply(new PointerInput(PointerAction.Up, to.X, to.Y, Shift: shift));
    }

    public static TheoryData<SelectionToolMode> MoveModes => [SelectionToolMode.MoveSelection, SelectionToolMode.MoveSelectedPixels];

    public static TheoryData<SelectionToolMode> AllModes =>
        [SelectionToolMode.Select, SelectionToolMode.MoveSelection, SelectionToolMode.MoveSelectedPixels];

    // Select

    [Fact]
    public void Select_WithShift_MarksOutASquare()
    {
        var editor = CreateEditor();

        Drag(editor, (2.5, 2.5), (12.5, 6.5), shift: true);

        Assert.Equal(new IntRect(2, 2, 11, 11), editor.Selection?.Bounds);
    }

    [Fact]
    public void Select_ADragInsideTheSelection_StartsANewOne_AndMovesNothing()
    {
        var editor = WithSquareSelected(SelectionToolMode.Select);
        var before = Snapshot(editor);

        Drag(editor, (5.5, 5.5), (15.5, 9.5));

        Assert.Equal(new Selection(new IntRect(5, 5, 11, 5), null), editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Theory]
    [InlineData(SelectionToolMode.Select, false)]
    [InlineData(SelectionToolMode.MoveSelection, false)]
    [InlineData(SelectionToolMode.MoveSelectedPixels, true)]
    public void OnlyMoveSelectedPixels_HasHandles(SelectionToolMode mode, bool handles)
    {
        var editor = WithSquareSelected(mode);
        var adornment = editor.Adornment!.Value;
        var (rx, ry) = adornment.RotateHandle(1);

        Assert.Equal(new IntRect(4, 4, 4, 4), adornment.Frame);
        Assert.Equal(handles, adornment.HasHandles);
        Assert.Equal(handles ? 8 : 0, adornment.Handles.Count());
        Assert.Equal(handles ? FrameHandle.Rotate : FrameHandle.None, adornment.HitTest(rx, ry, 1));
        Assert.Equal(handles ? FrameHandle.BottomRight : FrameHandle.Move, adornment.HitTest(7.9, 7.9, 1));
    }

    // Move selection

    [Fact]
    public void MoveSelection_MovesTheOutline_WithoutChangingPixels_OrHistory()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelection);
        var before = Snapshot(editor);

        Drag(editor, (5.5, 5.5), (15.5, 7.5));

        Assert.Equal(new Selection(new IntRect(14, 6, 4, 4), null), editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.True(editor.Overlay.IsEmpty);
        Assert.False(editor.History.CanUndo);

        // What is now selected is what is under the outline.
        Assert.All(editor.CopySelection()!.Pixels, p => Assert.Equal(Rgba.White, p));
    }

    [Fact]
    public void MoveSelection_PartlyOffTheCanvas_SelectsNothingThere_AndMovingBackRestoresIt()
    {
        var editor = CreateEditor();
        editor.SelectionSettings.Shape = SelectionShape.FreeForm;
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (7.5, 4.5), (4.5, 7.5)); // a triangle on the square
        var selected = editor.Selection!.Value;
        Script.Use(editor, SelectionToolMode.MoveSelection);

        Drag(editor, (4.5, 4.5), (-1.5, 4.5)); // by (-6, 0): two columns stay on the canvas

        Assert.Equal(new IntRect(-2, 4, 4, 4), editor.Selection?.Bounds);
        var copy = editor.CopySelection()!;
        Assert.Equal(Rgba.Transparent, copy.Pixels[0]);        // (-2, 4): off the canvas
        Assert.Equal(Rgba.White, copy.Pixels[2]);              // (0, 4)

        Drag(editor, (0.5, 4.5), (6.5, 4.5)); // back by (6, 0)

        Assert.Equal(selected, editor.Selection);
    }

    [Fact]
    public void MovingPixels_OfAnOutlinePartlyOffTheCanvas_LiftsOnlyTheOnCanvasPart()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelection);
        Drag(editor, (4.5, 4.5), (-1.5, 4.5)); // the outline at (-2, 4): columns 0 and 1 on the canvas
        Layer(editor).WriteRegion(new IntRect(0, 4, 2, 4), Enumerable.Repeat(Red, 8).ToArray());
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Drag(editor, (0.5, 6.5), (20.5, 16.5)); // by (20, 10), clear of the handles
        editor.CommitPending();

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(0, 4)); // lifted
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(18, 14));      // off the canvas when lifted: nothing
        Assert.Equal(Red, Layer(editor).GetPixel(20, 14));
        Assert.Equal(Red, Layer(editor).GetPixel(21, 17));
    }

    [Fact]
    public void Delete_OfAnOutlinePartlyOffTheCanvas_ClearsTheOnCanvasPart()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelection);
        Drag(editor, (4.5, 4.5), (-1.5, -1.5)); // the outline at (-2, -2)

        editor.DeleteSelection();

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(0, 0));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(1, 1));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 2));
    }

    [Fact]
    public void MoveSelection_CommitsFloatingPixelsFirst_ThenMovesTheOutlineAlone()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelection);
        editor.Transform(ImageTransform.FlipHorizontal); // floating

        Drag(editor, (5.5, 5.5), (15.5, 5.5));

        Assert.Equal(new Selection(new IntRect(14, 4, 4, 4), null), editor.Selection);
        Assert.Equal(Red, Layer(editor).GetPixel(4, 4)); // the flipped square, put down where it was
        Assert.True(editor.History.CanUndo);
    }

    // Move selected pixels

    [Fact]
    public void MoveSelectedPixels_StretchesWithTheHandles()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);

        Drag(editor, (8, 8), (12, 12));
        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(11, 11));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(12, 12));
    }

    // Both Move tools

    [Theory]
    [MemberData(nameof(MoveModes))]
    public void Shift_LocksADragToTheLongerAxis(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        Drag(editor, (5.5, 5.5), (15.5, 8.5), shift: true);
        Assert.Equal(new IntRect(14, 4, 4, 4), editor.Selection?.Bounds);

        Drag(editor, (15.5, 5.5), (17.5, 15.5), shift: true);
        Assert.Equal(new IntRect(14, 14, 4, 4), editor.Selection?.Bounds);
    }

    [Theory]
    [MemberData(nameof(MoveModes))]
    public void Nudge_MovesBySteps(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        Assert.True(editor.Nudge(1, 0));
        Assert.True(editor.Nudge(0, -1));
        Assert.True(editor.Nudge(10, 0));

        Assert.Equal(new IntRect(15, 3, 4, 4), editor.Selection?.Bounds);
        Assert.Equal(mode == SelectionToolMode.MoveSelectedPixels, editor.Selection?.FloatingPixels is not null);
    }

    [Fact]
    public void NudgingPixels_IsOneHistoryStep_WhenCommitted()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);
        var before = Snapshot(editor);
        for (var i = 0; i < 5; i++)
        {
            editor.Nudge(1, 0);
        }

        Assert.False(editor.History.CanUndo);
        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(9, 4));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Nudge_WithSelect_OrWithoutASelection_DoesNothing()
    {
        var editor = WithSquareSelected(SelectionToolMode.Select);
        Assert.False(editor.Nudge(1, 0));
        Assert.Equal(new IntRect(4, 4, 4, 4), editor.Selection?.Bounds);

        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        editor.CancelPending();
        Assert.False(editor.Nudge(1, 0));
        Assert.Null(editor.Selection);
    }

    [Theory]
    [MemberData(nameof(MoveModes))]
    public void WithNothingSelected_ADragDoesNothing(SelectionToolMode mode)
    {
        var editor = CreateEditor();
        Script.Use(editor, mode);
        var before = Snapshot(editor);

        Drag(editor, (2.5, 2.5), (12.5, 12.5));

        Assert.Null(editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Theory]
    [MemberData(nameof(MoveModes))]
    public void AClickOrDragOutsideTheSelection_DoesNothing(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);
        editor.Nudge(10, 0); // the outline or pixels at (14, 4)
        var selection = editor.Selection;
        var before = Snapshot(editor);

        Drag(editor, (25.5, 25.5), (25.5, 25.5));
        Drag(editor, (25.5, 25.5), (28.5, 20.5));

        Assert.Equal(selection, editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    // Committing

    [Theory]
    [InlineData(SelectionToolMode.Select)]
    [InlineData(SelectionToolMode.MoveSelection)]
    public void SwitchingAmongTheSelectionTools_CommitsFloatingPixels_AndKeepsTheOutlineWhereTheyLanded(SelectionToolMode next)
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);
        Drag(editor, (5.5, 5.5), (15.5, 7.5)); // by (10, 2)

        Script.Use(editor, next);

        Assert.Equal(new Selection(new IntRect(14, 6, 4, 4), null), editor.Selection);
        Assert.Equal(Red, Layer(editor).GetPixel(14, 6));
        Assert.True(editor.Overlay.IsEmpty);
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void SwitchingAmongTheSelectionTools_KeepsAFreeFormShape()
    {
        var editor = CreateEditor();
        editor.SelectionSettings.Shape = SelectionShape.FreeForm;
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (7.5, 4.5), (4.5, 7.5));
        var selection = editor.Selection;

        Script.Use(editor, SelectionToolMode.MoveSelection);
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Assert.Equal(selection, editor.Selection);
        Assert.False(editor.History.CanUndo);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void SwitchingToAnotherTool_CommitsAndClearsTheSelection(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);
        editor.Nudge(10, 0);
        Script.Use(editor, mode);

        editor.ActiveTool = new PencilTool();

        Assert.Null(editor.Selection);
        Assert.Null(editor.Adornment);
        Assert.Equal(Red, Layer(editor).GetPixel(14, 4));
    }

    [Fact]
    public void Enter_CommitsFloatingPixels_AndKeepsTheOutlineWhereTheyLanded()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);
        Drag(editor, (5.5, 5.5), (15.5, 7.5));

        editor.CommitPending(keepSelection: true);

        Assert.Equal(new Selection(new IntRect(14, 6, 4, 4), null), editor.Selection);
        Assert.Equal(Red, Layer(editor).GetPixel(14, 6));
        Assert.True(editor.History.CanUndo);
        Assert.True(editor.Overlay.IsEmpty);

        // Moving again lifts the pixels from where they landed.
        Drag(editor, (15.5, 7.5), (25.5, 7.5));
        editor.CommitPending();
        Assert.Equal(Red, Layer(editor).GetPixel(24, 6));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(14, 6));
    }

    [Fact]
    public void Esc_CancelsAPendingMove_ThenRemovesTheSelection()
    {
        var editor = WithSquareSelected(SelectionToolMode.MoveSelectedPixels);
        var before = Snapshot(editor);
        Drag(editor, (5.5, 5.5), (15.5, 7.5));
        editor.Nudge(1, 0);

        editor.CancelPending();

        Assert.Equal(new Selection(new IntRect(4, 4, 4, 4), null), editor.Selection);
        Assert.Equal(before, Snapshot(editor));
        Assert.True(editor.Overlay.IsEmpty);

        editor.CancelPending();

        Assert.Null(editor.Selection);
        Assert.False(editor.History.CanUndo);
    }

    // Commands

    [Theory]
    [MemberData(nameof(AllModes))]
    public void DeleteAndCopy_WorkWithAnySelectionTool(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        Assert.All(editor.CopySelection()!.Pixels, p => Assert.Equal(Red, p));
        editor.DeleteSelection();

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Null(editor.Selection);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Crop_WorksWithAnySelectionTool(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        editor.CropToSelection();

        Assert.Equal((4, 4), (editor.Document.Width, editor.Document.Height));
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void ImageOperations_ApplyToTheSelection_WithAnySelectionTool(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        editor.Transform(ImageTransform.RotateRight);
        Assert.Equal(new IntRect(4, 4, 4, 4), editor.Selection?.Bounds);
        editor.Resize(6, 4);
        Assert.Equal(new IntRect(4, 4, 6, 4), editor.Selection?.Bounds);
        editor.ResizeAndSkew(6, 4, 10, 0);
        Assert.NotEqual(6, editor.Selection?.Bounds.Width);
        editor.Rotate(30);

        Assert.Equal((32, 32), (editor.Document.Width, editor.Document.Height)); // the canvas is untouched
        Assert.NotNull(editor.Selection?.FloatingPixels);
        Assert.Equal(mode, ((SelectionTool)editor.ActiveTool!).Mode);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void SelectAllAndInvert_SwitchToSelect(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        editor.InvertSelection();

        Assert.Equal(SelectionToolMode.Select, ((SelectionTool)editor.ActiveTool!).Mode);
        Assert.False(editor.Selection!.Value.Contains(4, 4));

        Script.Use(editor, mode);
        editor.SelectAll();

        Assert.Equal(SelectionToolMode.Select, ((SelectionTool)editor.ActiveTool!).Mode);
        Assert.Equal(new IntRect(0, 0, 32, 32), editor.Selection?.Bounds);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Paste_SwitchesToMoveSelectedPixels(SelectionToolMode mode)
    {
        var editor = WithSquareSelected(mode);

        editor.Paste(new PixelImage(4, 4, Enumerable.Repeat(Red, 16).ToArray()), 20, 20);

        Assert.Equal(SelectionToolMode.MoveSelectedPixels, ((SelectionTool)editor.ActiveTool!).Mode);
        Drag(editor, (22, 22), (26, 22)); // the pasted pixels move at once
        Assert.Equal(new IntRect(24, 20, 4, 4), editor.Selection?.Bounds);
    }

    [Fact]
    public void Transparent_StillAppliesToMovedPixels()
    {
        var editor = CreateEditor();
        editor.SelectionSettings.Transparent = true; // colour 2 is white
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (9.5, 9.5)); // the square in a white border
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);

        Drag(editor, (5.5, 5.5), (15.5, 5.5));
        editor.CommitPending();

        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 2)); // white was not lifted
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(Red, Layer(editor).GetPixel(14, 4));
    }
}
