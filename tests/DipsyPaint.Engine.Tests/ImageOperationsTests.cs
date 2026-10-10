using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class ImageOperationsTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>A pixel whose value encodes its position, so any permutation is visible.</summary>
    private static Rgba Marker(int x, int y) => new((byte)(x * 10), (byte)(y * 10), 7, 255);

    /// <summary>A <paramref name="width"/>×<paramref name="height"/> document whose pixels are <see cref="Marker"/>s.</summary>
    private static DocumentEditor CreateEditor(int width = 3, int height = 2, ITool? tool = null)
    {
        var editor = new DocumentEditor(Document.CreateBlank(width, height)) { ActiveTool = tool, HandleTolerance = 0.25 };
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                layer.SetPixel(x, y, Marker(x, y));
            }
        }

        editor.Compositor.Update(new IntRect(0, 0, width, height));
        return editor;
    }

    private static Rgba[] Snapshot(IPixelAccess pixels)
    {
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    private static Rgba[] Snapshot(DocumentEditor editor) => Snapshot(editor.Document.ActiveLayer.Pixels);

    private static (int, int) SizeOf(DocumentEditor editor) => (editor.Document.Width, editor.Document.Height);

    [Theory]
    // 3×2 source, read row by row: (0,0) (1,0) (2,0) / (0,1) (1,1) (2,1).
    [InlineData(ImageTransform.RotateRight, 2, 3, new[] { 0, 1, 0, 0, 1, 1, 1, 0, 2, 1, 2, 0 })]
    [InlineData(ImageTransform.RotateLeft, 2, 3, new[] { 2, 0, 2, 1, 1, 0, 1, 1, 0, 0, 0, 1 })]
    [InlineData(ImageTransform.Rotate180, 3, 2, new[] { 2, 1, 1, 1, 0, 1, 2, 0, 1, 0, 0, 0 })]
    [InlineData(ImageTransform.FlipVertical, 3, 2, new[] { 0, 1, 1, 1, 2, 1, 0, 0, 1, 0, 2, 0 })]
    [InlineData(ImageTransform.FlipHorizontal, 3, 2, new[] { 2, 0, 1, 0, 0, 0, 2, 1, 1, 1, 0, 1 })]
    public void Transform_MovesEveryPixel_AndResizesTheCanvasAndComposite(
        ImageTransform transform, int width, int height, int[] sourceCoordinates)
    {
        var editor = CreateEditor();

        editor.Transform(transform);

        var expected = sourceCoordinates.Chunk(2).Select(c => Marker(c[0], c[1])).ToArray();
        Assert.Equal((width, height), SizeOf(editor));
        Assert.Equal(expected, Snapshot(editor));
        Assert.Equal((width, height), (editor.Compositor.Width, editor.Compositor.Height));
        Assert.Equal(expected, editor.Compositor.Pixels.ToArray());
    }

    [Theory]
    [InlineData(ImageTransform.RotateRight)]
    [InlineData(ImageTransform.RotateLeft)]
    [InlineData(ImageTransform.Rotate180)]
    [InlineData(ImageTransform.FlipVertical)]
    [InlineData(ImageTransform.FlipHorizontal)]
    public void Transform_IsOneStep_ThatUndoesAndRedoesExactly_WithoutStoringPixels(ImageTransform transform)
    {
        var editor = CreateEditor(5, 3);
        var before = Snapshot(editor);
        editor.Transform(transform);
        var after = Snapshot(editor);
        var afterSize = SizeOf(editor);

        editor.Undo();

        Assert.Equal((5, 3), SizeOf(editor));
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(before, editor.Compositor.Pixels.ToArray());
        Assert.False(editor.History.CanUndo);

        editor.Redo();

        Assert.Equal(afterSize, SizeOf(editor));
        Assert.Equal(after, Snapshot(editor));
        Assert.Equal(0, editor.History.SizeBytes);
    }

    [Fact]
    public void Transform_AppliesToEveryLayer()
    {
        var editor = CreateEditor();
        var top = editor.Document.AddLayer();
        top.Pixels.SetPixel(0, 0, Red);

        editor.Transform(ImageTransform.RotateRight);

        Assert.All(editor.Document.Layers, l => Assert.Equal((2, 3), (l.Pixels.Width, l.Pixels.Height)));
        Assert.Equal(Red, top.Pixels.GetPixel(1, 0));
        Assert.Equal(Marker(0, 0), editor.Document.Layers[0].Pixels.GetPixel(1, 0));
    }

    [Fact]
    public void SizeChanged_IsRaisedWhenTheCanvasSizeChanges_IncludingOnUndoAndRedo()
    {
        var editor = CreateEditor();
        var raised = 0;
        editor.SizeChanged += (_, _) => raised++;

        editor.Transform(ImageTransform.Rotate180); // same size
        Assert.Equal(0, raised);

        editor.Transform(ImageTransform.RotateLeft);
        editor.Undo();
        editor.Redo();
        Assert.Equal(3, raised);
    }

    [Fact]
    public void Transform_CommitsAPendingShapeFirst()
    {
        var editor = CreateEditor(8, 4, new ShapeTool(new ShapeSettings { Kind = ShapeKind.Line }));
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (7.5, 0.5));

        editor.Transform(ImageTransform.FlipVertical);

        Assert.Null(editor.Adornment);
        Assert.Equal(Rgba.Black, editor.Document.ActiveLayer.Pixels.GetPixel(3, 3));
        editor.Undo();
        editor.Undo();
        Assert.Equal(Marker(3, 0), editor.Document.ActiveLayer.Pixels.GetPixel(3, 0));
    }

    [Fact]
    public void WithASelection_TransformRotatesItInPlace_KeepingItsTopLeftCorner_AndItStaysPending()
    {
        var editor = CreateEditor(8, 8, new SelectionTool());
        Script.Stroke(editor, PointerButton.Left, (1.5, 1.5), (3.5, 2.5)); // 3×2 at (1, 1)
        var before = Snapshot(editor);

        editor.Transform(ImageTransform.RotateRight);

        Assert.Equal((8, 8), SizeOf(editor));
        Assert.Equal(new IntRect(1, 1, 2, 3), editor.Selection?.Bounds);
        Assert.Equal(Rgba.Transparent, editor.Document.ActiveLayer.Pixels.GetPixel(3, 1)); // the lifted hole
        Assert.Equal(Marker(1, 2), editor.Compositor.Pixels[1 * 8 + 1]);                // shown rotated
        Assert.False(editor.History.CanUndo);

        editor.CommitPending();

        Assert.Equal(Marker(1, 2), editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
        Assert.Equal(Marker(1, 1), editor.Document.ActiveLayer.Pixels.GetPixel(2, 1));
        Assert.Equal(Marker(3, 1), editor.Document.ActiveLayer.Pixels.GetPixel(2, 3));
        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Crop_KeepsTheSelectedPixels_AsOneStep_ThatUndoesAndRedoesExactly()
    {
        var editor = CreateEditor(8, 6, new SelectionTool());
        var before = Snapshot(editor);
        Script.Stroke(editor, PointerButton.Left, (2.5, 1.5), (5.5, 3.5)); // 4×3 at (2, 1)

        editor.CropToSelection();

        Assert.Equal((4, 3), SizeOf(editor));
        Assert.Null(editor.Selection);
        Assert.Equal(Marker(2, 1), editor.Document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Equal(Marker(5, 3), editor.Document.ActiveLayer.Pixels.GetPixel(3, 2));
        Assert.Equal((4, 3), (editor.Compositor.Width, editor.Compositor.Height));
        Assert.Equal(4L * 8 * 6, editor.History.SizeBytes); // the larger, uncropped version

        editor.Undo();
        Assert.Equal((8, 6), SizeOf(editor));
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(before, editor.Compositor.Pixels.ToArray());

        editor.Redo();
        Assert.Equal((4, 3), SizeOf(editor));
        Assert.Equal(Marker(5, 3), editor.Document.ActiveLayer.Pixels.GetPixel(3, 2));
    }

    [Fact]
    public void Crop_AfterMovingTheSelection_CommitsItFirst_AndCropsToWhereItIsNow()
    {
        var editor = CreateEditor(8, 6, new SelectionTool());
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (1.5, 1.5));  // 2×2 at (0, 0)
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (4.5, 2.5));  // moved to (4, 2)

        editor.CropToSelection();

        Assert.Equal((2, 2), SizeOf(editor));
        Assert.Equal(Marker(0, 0), editor.Document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Equal(Marker(1, 1), editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));

        editor.Undo(); // the crop
        Assert.Equal((8, 6), SizeOf(editor));
        Assert.Equal(Marker(0, 0), editor.Document.ActiveLayer.Pixels.GetPixel(4, 2));
        editor.Undo(); // the move
        Assert.Equal(Marker(4, 2), editor.Document.ActiveLayer.Pixels.GetPixel(4, 2));
    }

    [Fact]
    public void Crop_ToASelectionPartlyOffTheCanvas_KeepsTheOnCanvasPart()
    {
        var editor = CreateEditor(8, 6, new SelectionTool());
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (1.5, 1.5));
        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (7.5, 5.5)); // moved to (7, 5): one pixel on the canvas

        editor.CropToSelection();

        Assert.Equal((1, 1), SizeOf(editor));
        Assert.Equal(Marker(0, 0), editor.Document.ActiveLayer.Pixels.GetPixel(0, 0));
    }

    [Fact]
    public void Crop_WithoutASelection_DoesNothing()
    {
        var editor = CreateEditor(8, 6, new SelectionTool());

        editor.CropToSelection();

        Assert.Equal((8, 6), SizeOf(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Crop_AppliesToEveryLayer()
    {
        var editor = CreateEditor(8, 6, new SelectionTool());
        var top = editor.Document.AddLayer();
        top.Pixels.SetPixel(3, 2, Red);
        editor.Document.ActiveLayer = editor.Document.Layers[0];
        Script.Stroke(editor, PointerButton.Left, (2.5, 1.5), (5.5, 3.5));

        editor.CropToSelection();

        Assert.All(editor.Document.Layers, l => Assert.Equal((4, 3), (l.Pixels.Width, l.Pixels.Height)));
        Assert.Equal(Red, top.Pixels.GetPixel(1, 1));
    }

    [Fact]
    public void Resize_ResamplesEveryLayer_AsOneStep_ThatUndoesAndRedoesExactly()
    {
        var editor = CreateEditor(6, 4);
        editor.Document.AddLayer();
        var before = Snapshot(editor.Document.Layers[0].Pixels);

        Assert.Equal(ResizeResult.Resized, editor.Resize(12, 2));

        Assert.All(editor.Document.Layers, l => Assert.Equal((12, 2), (l.Pixels.Width, l.Pixels.Height)));
        Assert.Equal((12, 2), (editor.Compositor.Width, editor.Compositor.Height));
        var after = Snapshot(editor.Document.Layers[0].Pixels);

        editor.Undo();
        Assert.Equal((6, 4), SizeOf(editor));
        Assert.Equal(before, Snapshot(editor.Document.Layers[0].Pixels));
        Assert.Equal(Enumerable.Repeat(Rgba.Transparent, 24), Snapshot(editor.Document.Layers[1].Pixels));

        editor.Redo();
        Assert.Equal(after, Snapshot(editor.Document.Layers[0].Pixels));
    }

    [Fact]
    public void Resize_ToTheSameSize_RecordsNothing()
    {
        var editor = CreateEditor(6, 4);

        Assert.Equal(ResizeResult.Resized, editor.Resize(6, 4));

        Assert.False(editor.History.CanUndo);
    }

    [Theory]
    [InlineData(Document.MaxSide + 1, 10)]
    [InlineData(10, Document.MaxSide + 1)]
    public void Resize_BeyondTheMaximumSide_IsRefused_AndChangesNothing(int width, int height)
    {
        var editor = CreateEditor(6, 4);
        var before = Snapshot(editor);

        Assert.Equal(ResizeResult.TooLarge, editor.Resize(width, height));

        Assert.Equal((6, 4), SizeOf(editor));
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Resize_ToTheMaximumSide_IsAllowed()
    {
        var editor = CreateEditor(2, 1);

        Assert.Equal(ResizeResult.Resized, editor.Resize(Document.MaxSide, 1));

        Assert.Equal((Document.MaxSide, 1), SizeOf(editor));
    }

    [Fact]
    public void Resize_OverTheMemoryBudget_IsRefused_AndChangesNothing()
    {
        var editor = CreateEditor(6, 4);

        // One layer: layer, intermediate, composite and on-screen bitmap, 4 bytes per pixel each.
        Assert.Equal(ResizeResult.OverMemoryBudget, editor.Resize(100, 100, new MemoryBudget(4 * 4 * 100 * 100 - 1)));
        Assert.Equal(ResizeResult.Resized, editor.Resize(100, 100, new MemoryBudget(4 * 4 * 100 * 100)));

        Assert.Equal((100, 100), SizeOf(editor));
    }

    [Fact]
    public void Resize_WithASelection_ResamplesOnlyTheSelection_WhichStaysPending()
    {
        var editor = CreateEditor(8, 8, new SelectionTool());
        Script.Stroke(editor, PointerButton.Left, (1.5, 1.5), (2.5, 2.5)); // 2×2 at (1, 1)

        Assert.Equal(ResizeResult.Resized, editor.Resize(4, 6));

        Assert.Equal((8, 8), SizeOf(editor));
        Assert.Equal(new IntRect(1, 1, 4, 6), editor.Selection?.Bounds);
        editor.CommitPending();
        Assert.Equal(Marker(1, 1), editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
        Assert.Equal(Marker(2, 2), editor.Document.ActiveLayer.Pixels.GetPixel(4, 6));
    }

    [Fact]
    public void Resize_ShrinkingAveragesSourcePixels_AndTransparencyDoesNotDarkenColours()
    {
        var editor = CreateEditor(2, 1);
        var layer = editor.Document.ActiveLayer.Pixels;
        layer.SetPixel(0, 0, Red);
        layer.SetPixel(1, 0, Rgba.Transparent);

        editor.Resize(1, 1);

        // Half-covered red, premultiplied: still pure red when un-premultiplied.
        Assert.Equal(new Rgba(128, 0, 0, 128), layer.GetPixel(0, 0));
    }

    [Fact]
    public void Resize_KeepsAUniformColourUniform()
    {
        var editor = new DocumentEditor(Document.CreateBlank(5, 3));

        editor.Resize(13, 2);

        Assert.All(Snapshot(editor), p => Assert.Equal(Rgba.White, p));
    }

    [Fact]
    public void Redo_CommitsALiftedSelectionFirst_SoNothingIsRedoneUnderIt()
    {
        var editor = CreateEditor(8, 8, new SelectionTool());
        editor.Transform(ImageTransform.RotateRight);
        editor.Undo();
        Script.Stroke(editor, PointerButton.Left, (1.5, 1.5), (2.5, 2.5));
        Script.Stroke(editor, PointerButton.Left, (1.5, 1.5), (5.5, 5.5)); // lifted and moved

        editor.Redo();

        Assert.Null(editor.Selection);
        Assert.False(editor.History.CanRedo);
        Assert.Equal(Marker(1, 1), editor.Document.ActiveLayer.Pixels.GetPixel(5, 5));
    }
}
