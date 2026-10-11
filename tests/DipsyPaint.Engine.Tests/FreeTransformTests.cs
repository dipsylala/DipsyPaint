using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>Freeform rotate and skew (ticket 23): of selections, shapes, text and the whole picture.</summary>
public class FreeTransformTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static Rgba Marker(int x, int y) => new((byte)(x * 10), (byte)(y * 10), 7, 255);

    /// <summary>A document whose pixels are <see cref="Marker"/>s.</summary>
    private static DocumentEditor CreateEditor(int width, int height, ITool? tool = null)
    {
        var editor = new DocumentEditor(Document.CreateBlank(width, height)) { ActiveTool = tool };
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

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    private static (int, int) SizeOf(DocumentEditor editor) => (editor.Document.Width, editor.Document.Height);

    /// <summary>Asserts that one undo restores <paramref name="before"/> and leaves nothing to undo.</summary>
    private static void AssertOneStep(DocumentEditor editor, (int, int) size, Rgba[] before)
    {
        editor.Undo();
        Assert.Equal(size, SizeOf(editor));
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Rotation_IsClockwise_AndExactAtQuarterTurns()
    {
        Assert.Equal(new LinearTransform(0, -1, 1, 0), LinearTransform.Rotation(90));
        Assert.Equal(new LinearTransform(-1, 0, 0, -1), LinearTransform.Rotation(-180));
        Assert.True(LinearTransform.Rotation(360).IsIdentity);
        Assert.True(LinearTransform.Rotation(30).Then(LinearTransform.Rotation(-30)).IsIdentity);
        var (x, y) = LinearTransform.Rotation(90).Apply(1, 0);
        Assert.Equal((0.0, 1.0), (x, y)); // right of the centre goes below it: clockwise on screen (y down)
    }

    [Fact]
    public void Skew_ShiftsTheTopRight_AndRaisesTheRightSide()
    {
        Assert.True(LinearTransform.Skew(0, 0).IsIdentity);
        Assert.Equal(new LinearTransform(1, -1, 0, 1), LinearTransform.Skew(45, 0));
        Assert.Equal(new LinearTransform(1, 0, -1, 1), LinearTransform.Skew(0, 45));
        Assert.Throws<ArgumentOutOfRangeException>(() => LinearTransform.Skew(90, 0));
    }

    [Theory]
    [InlineData(30, 20, 10, 23, 19)]   // 17.32 + 5 by 10 + 8.66
    [InlineData(90, 20, 10, 10, 20)]
    [InlineData(45, 10, 10, 15, 15)]   // 14.14
    [InlineData(180, 7, 3, 7, 3)]
    public void SizeOf_IsTheUprightBoxAroundTheTurnedRectangle(double degrees, int width, int height, int newWidth, int newHeight)
    {
        Assert.Equal((newWidth, newHeight), LinearTransform.Rotation(degrees).SizeOf(width, height));
    }

    [Fact]
    public void Rotate_WithoutASelection_ByAQuarterTurn_EqualsRotateRight()
    {
        var expected = CreateEditor(5, 3);
        expected.Transform(ImageTransform.RotateRight);
        var editor = CreateEditor(5, 3);

        Assert.Equal(ResizeResult.Resized, editor.Rotate(90));

        Assert.Equal(SizeOf(expected), SizeOf(editor));
        Assert.Equal(Snapshot(expected), Snapshot(editor));
    }

    [Fact]
    public void Rotate_WithoutASelection_GrowsTheCanvas_WhiteWhereThePictureNoLongerReaches_AsOneStep()
    {
        var editor = CreateEditor(20, 10);
        var before = Snapshot(editor);

        editor.Rotate(30);

        Assert.Equal((23, 19), SizeOf(editor));
        Assert.Equal((23, 19), (editor.Compositor.Width, editor.Compositor.Height));
        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(22, 18));
        AssertOneStep(editor, (20, 10), before);
    }

    [Fact]
    public void Rotate_UpperLayersStayTransparentWhereThePictureNoLongerReaches()
    {
        var editor = CreateEditor(20, 10);
        editor.AddLayer();

        editor.Rotate(45);

        Assert.Equal(Rgba.Transparent, editor.Document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Equal(Rgba.White, editor.Document.Layers[0].Pixels.GetPixel(0, 0));
    }

    [Fact]
    public void Rotate_BeyondTheSizeLimit_IsRefused()
    {
        var editor = new DocumentEditor(Document.CreateBlank(100, Document.MaxSide));
        var wide = new DocumentEditor(Document.CreateBlank(Document.MaxSide - 80, 2));

        Assert.Equal(ResizeResult.TooLarge, editor.Rotate(0.3)); // 16384 cos 0.3° + 100 sin 0.3° > 16384
        Assert.Equal(ResizeResult.TooLarge, wide.ResizeAndSkew(wide.Document.Width, 2, 89, 0));
        Assert.Equal(ResizeResult.OverMemoryBudget, new DocumentEditor(Document.CreateBlank(10, 10)).Rotate(45, new MemoryBudget(100)));
        Assert.Equal((100, Document.MaxSide), SizeOf(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Rotate_WithLayers_AlsoCountsTheCompositorCaches()
    {
        var editor = new DocumentEditor(Document.CreateBlank(10, 10));
        editor.AddLayer();

        // Two layers, intermediate, composite, on-screen bitmap and the two caches: 7 bitmaps.
        Assert.Equal(ResizeResult.OverMemoryBudget, editor.Rotate(180, new MemoryBudget(7 * 4 * 10 * 10 - 1)));
        Assert.Equal(ResizeResult.Resized, editor.Rotate(180, new MemoryBudget(7 * 4 * 10 * 10)));
    }

    [Fact]
    public void Rotate_ASelection_TurnsItAboutItsCentre_AndStaysPending_ThenCommitsAsOneStep()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        var before = Snapshot(editor);
        editor.Select(new IntRect(10, 15, 20, 10));

        editor.Rotate(90);

        var selection = editor.Selection!.Value;
        Assert.Equal(new IntRect(15, 10, 10, 20), selection.Bounds); // same centre, sides swapped
        Assert.Equal(Marker(10, 24), selection.FloatingPixels![0]);   // the bottom-left corner went to the top-left
        Assert.Equal(Rgba.Transparent, editor.Document.ActiveLayer.Pixels.GetPixel(12, 16)); // lifted

        editor.CommitPending();
        Assert.Equal(Marker(10, 24), editor.Document.ActiveLayer.Pixels.GetPixel(15, 10));
        AssertOneStep(editor, (40, 40), before);
    }

    [Fact]
    public void Rotate_ASelection_GrowsItsFrame_AndLeavesTheCornersOutsideItsShape()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        editor.Select(new IntRect(10, 10, 10, 10));

        editor.Rotate(45);

        var selection = editor.Selection!.Value;
        Assert.Equal(new IntRect(8, 8, 15, 15), selection.Bounds);
        Assert.NotNull(selection.Mask);
        Assert.False(selection.Contains(8, 8));
        Assert.Equal(Rgba.Transparent, selection.FloatingPixels![0]);
        Assert.True(selection.Contains(15, 15));
    }

    [Fact]
    public void Rotate_ASelectionAgain_StartsFromTheUnturnedPixels_SoTurningBackRestoresThem()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        editor.Select(new IntRect(10, 10, 12, 8));
        var original = editor.CopySelection()!;

        editor.Rotate(37);
        editor.Rotate(-37);

        var selection = editor.Selection!.Value;
        Assert.Equal(new IntRect(10, 10, 12, 8), selection.Bounds);
        Assert.Equal(original.Pixels, selection.FloatingPixels);
    }

    [Fact]
    public void RotateHandle_IsAboveTheFrame_AndDraggingItTurnsTheSelection()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        editor.HandleTolerance = 1;
        editor.Select(new IntRect(10, 15, 20, 10));
        var adornment = editor.Adornment!.Value;
        var (hx, hy) = adornment.RotateHandle(editor.HandleTolerance);
        Assert.Equal((20.0, 11.0), (hx, hy));
        Assert.Equal(FrameHandle.Rotate, adornment.HitTest(hx, hy, editor.HandleTolerance));

        // From straight above the centre (20, 20) to straight right of it: a quarter turn clockwise.
        Script.Stroke(editor, PointerButton.Left, (hx, hy), (25, 12), (30, 20));

        Assert.Equal(new IntRect(15, 10, 10, 20), editor.Selection!.Value.Bounds);
        Assert.False(editor.History.CanUndo); // pending until committed
    }

    [Fact]
    public void RotateHandle_WithShift_SnapsTo15DegreeSteps()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        editor.HandleTolerance = 1;
        editor.Select(new IntRect(10, 10, 20, 20));
        var (hx, hy) = editor.Adornment!.Value.RotateHandle(1);

        // To 88° clockwise of straight up (just short of a quarter turn): snaps to 90°.
        var to = (X: 20 + 10 * Math.Sin(88 * Math.PI / 180), Y: 20 - 10 * Math.Cos(88 * Math.PI / 180));
        editor.Apply(new PointerInput(PointerAction.Down, hx, hy, PointerButton.Left));
        editor.Apply(new PointerInput(PointerAction.Up, to.X, to.Y, PointerButton.Left, Shift: true));

        Assert.Equal(new IntRect(10, 10, 20, 20), editor.Selection!.Value.Bounds);
        Assert.Equal(Marker(10, 29), editor.Selection!.Value.FloatingPixels![0]);
    }

    [Fact]
    public void Cancel_AfterRotatingASelection_RestoresTheLayer()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        var before = Snapshot(editor);
        editor.Select(new IntRect(10, 10, 10, 10));
        editor.Rotate(30);

        editor.CancelPending();

        Assert.Equal(before, Snapshot(editor));
        Assert.Null(editor.Selection);
    }

    [Fact]
    public void ResizeAndSkew_WithoutASelection_GrowsTheCanvas_TopShiftedRight_AsOneStep()
    {
        var editor = new DocumentEditor(Document.CreateBlank(10, 10));
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                editor.Document.ActiveLayer.Pixels.SetPixel(x, y, Red); // a red band down the left
            }
        }

        var before = Snapshot(editor);

        Assert.Equal(ResizeResult.Resized, editor.ResizeAndSkew(10, 10, 20, 0));

        // 10 + 10 × tan 20° = 13.6 wide: the top row starts 3.64 px in, the bottom row 0.36 px in.
        Assert.Equal((14, 10), SizeOf(editor));
        var layer = editor.Document.ActiveLayer.Pixels;
        Assert.Equal(Rgba.White, layer.GetPixel(0, 0));
        Assert.Equal(Red, layer.GetPixel(4, 0));
        Assert.Equal(Red, layer.GetPixel(1, 9));
        Assert.Equal(Rgba.White, layer.GetPixel(13, 9));
        AssertOneStep(editor, (10, 10), before);
    }

    [Fact]
    public void ResizeAndSkew_Vertically_RaisesTheRightSide()
    {
        var editor = new DocumentEditor(Document.CreateBlank(10, 10));
        for (var y = 7; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                editor.Document.ActiveLayer.Pixels.SetPixel(x, y, Red); // a red band along the bottom
            }
        }

        editor.ResizeAndSkew(10, 10, 0, 45);

        Assert.Equal((10, 20), SizeOf(editor));
        var layer = editor.Document.ActiveLayer.Pixels;
        Assert.Equal(Red, layer.GetPixel(0, 18));
        Assert.Equal(Red, layer.GetPixel(9, 9));
        Assert.Equal(Rgba.White, layer.GetPixel(9, 18));
    }

    [Fact]
    public void ResizeAndSkew_ResizesFirst_AndWithoutSkewIsAResize()
    {
        var editor = CreateEditor(10, 10);

        editor.ResizeAndSkew(20, 10, 0, -45);
        Assert.Equal((20, 30), SizeOf(editor));

        editor.ResizeAndSkew(5, 5, 0, 0);
        Assert.Equal((5, 5), SizeOf(editor));
    }

    [Fact]
    public void ResizeAndSkew_ASelection_KeepsItsTopLeft_AndStaysPending()
    {
        var editor = CreateEditor(40, 40, new SelectionTool());
        var before = Snapshot(editor);
        editor.Select(new IntRect(5, 5, 10, 10));

        editor.ResizeAndSkew(10, 10, 45, 0);

        Assert.Equal(new IntRect(5, 5, 20, 10), editor.Selection!.Value.Bounds);
        Assert.Equal((40, 40), SizeOf(editor));
        Assert.False(editor.History.CanUndo);
        editor.CommitPending();
        AssertOneStep(editor, (40, 40), before);
    }

    [Fact]
    public void Rotate_APendingShape_TurnsItsOutline_KeepingEdgesHard_AndCommitsAsOneStep()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = ShapeStyle.Solid, Fill = ShapeStyle.Solid, Size = 1 };
        var editor = new DocumentEditor(Document.CreateBlank(60, 60)) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red, SecondaryColor = Blue };
        var before = Snapshot(editor);
        Script.Stroke(editor, PointerButton.Left, (20.5, 25.5), (39.5, 34.5)); // 20×10

        editor.Rotate(90);
        Assert.Equal(new Adornment(new IntRect(25, 20, 10, 20)), editor.Adornment);

        editor.Rotate(-45); // 45° from upright
        var frame = editor.Adornment!.Value.Frame;
        Assert.Equal((22, 22), (frame.Width, frame.Height)); // (20 + 10) × cos 45° = 21.2
        Assert.All(editor.Compositor.Pixels.ToArray(), p => Assert.Contains(p, new[] { Red, Blue, Rgba.White }));

        editor.CommitPending();
        Assert.Equal(Blue, editor.Document.ActiveLayer.Pixels.GetPixel(30, 30));
        AssertOneStep(editor, (60, 60), before);
    }

    [Fact]
    public void RotateHandle_OnALine_TurnsItAboutItsMiddle()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Line, Size = 1 };
        var editor = new DocumentEditor(Document.CreateBlank(60, 60)) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red, HandleTolerance = 1 };
        Script.Stroke(editor, PointerButton.Left, (20.5, 30.5), (40.5, 30.5)); // horizontal, 21 px
        var (hx, hy) = editor.Adornment!.Value.RotateHandle(1);

        Script.Stroke(editor, PointerButton.Left, (hx, hy), (40, 30.5)); // a quarter turn

        Assert.Equal(new Adornment(new IntRect(30, 20, 1, 21)), editor.Adornment);
        Assert.Equal(Red, editor.Compositor.Pixels[20 * 60 + 30]);
        Assert.Equal(Red, editor.Compositor.Pixels[40 * 60 + 30]);
    }

    [Fact]
    public void Rotate_APendingPolygon_TurnsItsCorners()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Polygon, Size = 1 };
        var editor = new DocumentEditor(Document.CreateBlank(60, 60)) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red };
        Script.Stroke(editor, PointerButton.Left, (10.5, 10.5), (30.5, 10.5));
        Script.Stroke(editor, PointerButton.Left, (30.5, 20.5));
        Script.Stroke(editor, PointerButton.Left, (10.5, 10.5)); // closes
        Assert.Equal(new IntRect(10, 10, 21, 11), editor.Adornment!.Value.Frame);

        editor.Rotate(180);

        Assert.Equal(new IntRect(10, 10, 21, 11), editor.Adornment!.Value.Frame);
        Assert.Equal(Red, editor.Compositor.Pixels[20 * 60 + 10]); // the corner at (30, 20) is now at (10, 10)'s opposite
    }

    [Fact]
    public void Rotate_APendingTextBox_TurnsTheRenderedText_AndStopsEditing()
    {
        var tool = new TextTool(new TextSettings(), new SolidRenderer());
        var editor = new DocumentEditor(Document.CreateBlank(100, 100)) { ActiveTool = tool, PrimaryColor = Red };
        var before = Snapshot(editor);
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5), (29.5, 29.5)); // a 20×10 box
        tool.Text = "x";
        editor.RefreshPending();
        Assert.Equal(new IntRect(10, 20, 20, 10), tool.TextBounds);

        editor.Rotate(90);

        Assert.Null(tool.TextBounds);
        Assert.Equal(Red, editor.Compositor.Pixels[16 * 100 + 15]); // now 10×20 about the same centre (20, 25)
        Assert.Equal(Rgba.White, editor.Compositor.Pixels[20 * 100 + 10]);
        editor.CommitPending();
        Assert.Equal(Red, editor.Document.ActiveLayer.Pixels.GetPixel(15, 16));
        AssertOneStep(editor, (100, 100), before);
    }

    /// <summary>Renders any text as a box filled with the foreground colour.</summary>
    private sealed class SolidRenderer : ITextRenderer
    {
        public PixelImage Render(TextRequest request)
        {
            var pixels = new Rgba[request.Width * request.MinHeight];
            Array.Fill(pixels, request.Foreground);
            return new PixelImage(request.Width, request.MinHeight, pixels);
        }
    }
}
