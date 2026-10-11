using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>The magic wand: a selection of the contiguous area of similar colour (ticket 22, ADR-0011).</summary>
public class MagicWandTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba NearRed = new(230, 0, 0, 255);  // 25 from red: within 12% (31 per channel)
    private static readonly Rgba FarRed = new(223, 0, 0, 255);   // 32 from red: beyond 12%

    /// <summary>
    /// A white 32×32 document with a red 8×8 square at (4, 4), a near-red column at x = 12 and a
    /// far-red column at x = 13 beside it (rows 4–11), and a separate red 4×4 square at (20, 20);
    /// the selection tool is active with the magic wand at <paramref name="tolerance"/> percent.
    /// </summary>
    private static DocumentEditor CreateEditor(int tolerance = 0)
    {
        var settings = new SelectionSettings { Shape = SelectionShape.MagicWand, Tolerance = tolerance };
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { SelectionSettings = settings, HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(settings);
        var layer = Layer(editor);
        layer.WriteRegion(new IntRect(4, 4, 8, 8), Enumerable.Repeat(Red, 64).ToArray());
        layer.WriteRegion(new IntRect(12, 4, 1, 8), Enumerable.Repeat(NearRed, 8).ToArray());
        layer.WriteRegion(new IntRect(13, 4, 1, 8), Enumerable.Repeat(FarRed, 8).ToArray());
        layer.WriteRegion(new IntRect(20, 20, 4, 4), Enumerable.Repeat(Red, 16).ToArray());
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));
        return editor;
    }

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static void Click(DocumentEditor editor, double x, double y) => Script.Stroke(editor, PointerButton.Left, (x, y), (x, y));

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = new Rgba[32 * 32];
        Layer(editor).ReadRegion(new IntRect(0, 0, 32, 32), pixels);
        return pixels;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 31)]
    [InlineData(50, 128)]
    [InlineData(100, 255)]
    [InlineData(-5, 0)]
    [InlineData(150, 255)]
    public void ChannelTolerance_MapsPercentToTheLargestDifferencePerChannel(int percent, int expected) =>
        Assert.Equal(expected, MagicWand.ChannelTolerance(percent));

    [Fact]
    public void Tolerance_DefaultsTo12Percent_AndIsClamped()
    {
        var settings = new SelectionSettings();
        Assert.Equal(12, settings.Tolerance);
        settings.Tolerance = -1;
        Assert.Equal(0, settings.Tolerance);
        settings.Tolerance = 101;
        Assert.Equal(100, settings.Tolerance);
    }

    [Fact]
    public void ShapeChanged_IsRaisedWhenTheShapeChanges()
    {
        var settings = new SelectionSettings();
        var raised = 0;
        settings.ShapeChanged += (_, _) => raised++;

        settings.Shape = SelectionShape.MagicWand;
        settings.Shape = SelectionShape.MagicWand;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Click_SelectsTheContiguousAreaOfTheExactColour_WithoutWriting()
    {
        var editor = CreateEditor(tolerance: 0);
        var before = Snapshot(editor);

        Click(editor, 6.5, 6.5);

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(4, 4, 8, 8), selection.Bounds);
        Assert.Null(selection.Mask);                 // a whole rectangle
        Assert.False(selection.Contains(20, 20));    // same colour, not connected
        Assert.Null(selection.FloatingPixels);
        Assert.Equal(new Adornment(new IntRect(4, 4, 8, 8)), editor.Adornment);
        Assert.Equal(before, Snapshot(editor));
        editor.CommitPending();
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Click_IncludesColoursWithinTheTolerance_InEachChannel()
    {
        var editor = CreateEditor(tolerance: 12);

        Click(editor, 6.5, 6.5);

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(4, 4, 9, 8), selection.Bounds); // the near-red column, not the far-red one
        Assert.False(selection.Contains(13, 4));
    }

    [Fact]
    public void Click_AtFullTolerance_SelectsEverything()
    {
        var editor = CreateEditor(tolerance: 100);

        Click(editor, 0.5, 0.5);

        Assert.Equal(new Selection(new IntRect(0, 0, 32, 32), null), editor.Selection);
    }

    [Fact]
    public void Click_OnTheBackground_SelectsItsShape()
    {
        var editor = CreateEditor(tolerance: 0);

        Click(editor, 0.5, 0.5);

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(0, 0, 32, 32), selection.Bounds);
        Assert.NotNull(selection.Mask);
        Assert.True(selection.Contains(31, 31));
        Assert.False(selection.Contains(4, 4));
        Assert.False(selection.Contains(22, 22));
    }

    [Fact]
    public void Click_SamplesTheActiveLayer_NotTheComposite()
    {
        var editor = CreateEditor(tolerance: 0);
        Assert.True(editor.AddLayer()); // transparent, above, and active

        Click(editor, 6.5, 6.5);

        Assert.Equal(new Selection(new IntRect(0, 0, 32, 32), null), editor.Selection);
    }

    [Fact]
    public void Click_OutsideTheCanvas_SelectsNothing_AndCommitsTheSelection()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 6.5, 6.5);

        Click(editor, 40.5, 6.5);

        Assert.Null(editor.Selection);
        Assert.Null(editor.Adornment);
    }

    [Fact]
    public void Move_LiftsTheSelectedPixels_AsOneUndoableStep()
    {
        var editor = CreateEditor(tolerance: 12);
        var before = Snapshot(editor);
        Click(editor, 6.5, 6.5);

        Script.Stroke(editor, PointerButton.Left, (5.5, 5.5), (5.5, 17.5)); // move by (0, 12)
        editor.CommitPending();

        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(4, 4));
        Assert.Equal(FarRed, Layer(editor).GetPixel(13, 4));
        Assert.Equal(Red, Layer(editor).GetPixel(4, 16));
        Assert.Equal(NearRed, Layer(editor).GetPixel(12, 16));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(13, 16));

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Delete_ClearsOnlyTheSelectedPixels()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5);

        editor.DeleteSelection();

        Assert.Null(editor.Selection);
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(0, 0));
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(31, 31));
        Assert.Equal(Red, Layer(editor).GetPixel(4, 4));
        Assert.Equal(FarRed, Layer(editor).GetPixel(13, 11));
        Assert.Equal(Red, Layer(editor).GetPixel(23, 23));
        editor.Undo();
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(0, 0));
    }

    [Fact]
    public void Copy_IsTheBoundingBox_TransparentOutsideTheSelection()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5);

        var copy = Assert.IsType<PixelImage>(editor.CopySelection());

        Assert.Equal((32, 32), (copy.Width, copy.Height));
        Assert.Equal(Rgba.White, copy.Pixels[0]);
        Assert.Equal(Rgba.Transparent, copy.Pixels[4 * 32 + 4]);
        Assert.Equal(Rgba.Transparent, copy.Pixels[20 * 32 + 20]);
    }

    [Fact]
    public void Invert_SelectsEverythingElse()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5);

        editor.InvertSelection();

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(4, 4, 20, 20), selection.Bounds);
        Assert.True(selection.Contains(4, 4));
        Assert.True(selection.Contains(13, 11));
        Assert.True(selection.Contains(23, 23));
        Assert.False(selection.Contains(15, 15));
    }

    [Fact]
    public void PressOnAnUnselectedPixelInsideTheFrame_MakesANewSelection()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5); // the background: its frame is the whole canvas

        Click(editor, 21.5, 21.5);

        Assert.Equal(new Selection(new IntRect(20, 20, 4, 4), null), editor.Selection);
        Assert.False(editor.History.CanUndo); // nothing was lifted, so nothing was written
    }

    [Fact]
    public void PressOnAnUnselectedPixelInsideTheFrame_CommitsAMovedSelectionFirst()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5);                                             // the background
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (1.5, 0.5));   // move by (1, 0)

        Click(editor, 6.5, 6.5); // red, in the moved selection's hole

        // The moved background was committed (column 0 left transparent, white over the square's
        // left column), then the rest of the red square selected.
        Assert.Equal(Rgba.Transparent, Layer(editor).GetPixel(0, 0));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(4, 4));
        Assert.Equal(new Selection(new IntRect(5, 4, 7, 8), null), editor.Selection);
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void PressOnASelectedPixel_MovesTheSelection()
    {
        var editor = CreateEditor(tolerance: 0);
        Click(editor, 0.5, 0.5);

        Script.Stroke(editor, PointerButton.Left, (1.5, 1.5), (3.5, 1.5)); // move by (2, 0)

        var selection = Assert.IsType<Selection>(editor.Selection);
        Assert.Equal(new IntRect(2, 0, 32, 32), selection.Bounds);
        Assert.NotNull(selection.FloatingPixels);
    }

    [Fact]
    public void Select_UsesAScanlineSearch_SoALongWindingAreaCannotOverflowTheStack()
    {
        // A comb of 1 px walls joined alternately at the top and bottom: one path of about 500,000
        // pixels, deeper than any recursive flood could go on a 256 KB stack.
        const int size = 1000;
        var layer = Document.CreateBlank(size, size).ActiveLayer;
        for (var x = 1; x < size; x += 2)
        {
            var gap = x % 4 == 1 ? 0 : size - 1;
            for (var y = 0; y < size; y++)
            {
                if (y != gap)
                {
                    layer.Pixels.SetPixel(x, y, Rgba.Black);
                }
            }
        }

        Selection? selection = null;
        var thread = new Thread(() => selection = MagicWand.Select(layer.Pixels, 0, 0, 0), maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();

        var result = Assert.IsType<Selection>(selection);
        Assert.Equal(new IntRect(0, 0, size, size), result.Bounds);
        Assert.Equal(size / 2 * size + size / 2, result.Mask!.Count(selected => selected));
    }
}
