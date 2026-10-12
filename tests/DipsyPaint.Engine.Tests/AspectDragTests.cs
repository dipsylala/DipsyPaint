using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>Shift + dragging a corner handle keeps the frame's ratio of width to height (issue #113).</summary>
public class AspectDragTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    public static TheoryData<ShapeKind> AllShapes { get; } = [.. Enum.GetValues<ShapeKind>()];

    public static TheoryData<FrameHandle> Corners { get; } =
        [FrameHandle.TopLeft, FrameHandle.TopRight, FrameHandle.BottomRight, FrameHandle.BottomLeft];

    // Adornment.Drag: a 10×5 frame, (10, 10) to (19, 14).

    private static readonly (int X, int Y) A = (10, 10);
    private static readonly (int X, int Y) B = (19, 14);

    private static IntRect Dragged(FrameHandle handle, int dx, int dy)
    {
        var (a, b) = Adornment.Drag(A, B, handle, dx, dy, keepAspect: true);
        return Adornment.FrameOf(a, b);
    }

    [Theory]
    [InlineData(FrameHandle.BottomRight, 10, 1, 10, 10)]   // outwards by 10 across, 1 down: twice the size
    [InlineData(FrameHandle.TopLeft, -10, 0, 0, 5)]
    [InlineData(FrameHandle.TopRight, 10, -1, 10, 5)]
    [InlineData(FrameHandle.BottomLeft, -10, 1, 0, 10)]
    public void EachCorner_ScalesInProportion_AboutTheOppositeCorner(FrameHandle handle, int dx, int dy, int x, int y)
    {
        Assert.Equal(new IntRect(x, y, 20, 10), Dragged(handle, dx, dy));
    }

    [Fact]
    public void TheLargerProportionalChange_Wins()
    {
        Assert.Equal(new IntRect(10, 10, 30, 15), Dragged(FrameHandle.BottomRight, 2, 10)); // height ×3 beats width ×1.2
        Assert.Equal(new IntRect(10, 10, 10, 5), Dragged(FrameHandle.BottomRight, -5, 0));  // width ×0.5 loses to height ×1
        Assert.Equal(new IntRect(10, 10, 4, 2), Dragged(FrameHandle.BottomRight, -6, -4));  // width ×0.4 beats height ×0.2
    }

    [Fact]
    public void ACornerDraggedPastTheOppositeOne_FlipsTheFrame()
    {
        // From (19, 14) to (-9, 0): 20 wide and 11 tall the other way round; height ×2.2 wins.
        var (a, b) = Adornment.Drag(A, B, FrameHandle.BottomRight, -28, -14, keepAspect: true);

        Assert.Equal((10, 10), a);
        Assert.Equal((-11, 0), b);
        Assert.Equal(new IntRect(-11, 0, 22, 11), Adornment.FrameOf(a, b));

        // Past it along one axis only: flips that axis alone.
        Assert.Equal(((10, 10), (-9, 19)), Adornment.Drag(A, B, FrameHandle.BottomRight, -28, 0, keepAspect: true));
    }

    [Theory]
    [InlineData(FrameHandle.Top)]
    [InlineData(FrameHandle.Right)]
    [InlineData(FrameHandle.Bottom)]
    [InlineData(FrameHandle.Left)]
    [InlineData(FrameHandle.Move)]
    [InlineData(FrameHandle.Rotate)]
    public void OtherHandles_IgnoreTheConstraint(FrameHandle handle)
    {
        Assert.Equal(Adornment.Drag(A, B, handle, 7, 3), Adornment.Drag(A, B, handle, 7, 3, keepAspect: true));
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void EitherOrderOfCorners_GivesTheSameFrame(FrameHandle handle)
    {
        var forwards = Adornment.Drag(A, B, handle, 13, -4, keepAspect: true);
        var backwards = Adornment.Drag(B, A, handle, 13, -4, keepAspect: true);

        Assert.Equal(Adornment.FrameOf(forwards.A, forwards.B), Adornment.FrameOf(backwards.A, backwards.B));
    }

    // Move selected pixels: a 64×64 document with an 8×4 selection at (20, 20), red on its left half, blue on its right.

    private static DocumentEditor CreateSelection()
    {
        var editor = new DocumentEditor(Document.CreateBlank(64, 64)) { HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(editor.SelectionSettings);
        var pixels = Enumerable.Range(0, 32).Select(i => i % 8 < 4 ? Red : Blue).ToArray();
        editor.Document.ActiveLayer.Pixels.WriteRegion(new IntRect(20, 20, 8, 4), pixels);
        editor.Compositor.Update(new IntRect(0, 0, 64, 64));
        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), (27.5, 23.5));
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        return editor;
    }

    private static (double X, double Y) CornerOf(IntRect frame, FrameHandle handle) => handle switch
    {
        FrameHandle.TopLeft => (frame.X, frame.Y),
        FrameHandle.TopRight => (frame.Right, frame.Y),
        FrameHandle.BottomRight => (frame.Right, frame.Bottom),
        _ => (frame.X, frame.Bottom),
    };

    private static void Move(DocumentEditor editor, double x, double y, bool shift) =>
        editor.Apply(new PointerInput(PointerAction.Move, x, y, Shift: shift));

    [Theory]
    [MemberData(nameof(Corners))]
    public void MoveSelectedPixels_ShiftCornerDrag_ScalesThePixelsInProportion(FrameHandle handle)
    {
        var editor = CreateSelection();
        var frame = editor.Selection!.Value.Bounds;
        var (x, y) = CornerOf(frame, handle);
        var (sx, sy) = (x == frame.X ? -1 : 1, y == frame.Y ? -1 : 1); // outwards

        editor.Apply(new PointerInput(PointerAction.Down, x, y));
        Move(editor, x + sx * 8, y + sy, shift: true); // width ×2 beats height ×1.25
        editor.Apply(new PointerInput(PointerAction.Up, x + sx * 8, y + sy, Shift: true));

        // 16×8, the opposite corner where it was.
        var stretched = editor.Selection!.Value;
        Assert.Equal(16, stretched.Bounds.Width);
        Assert.Equal(8, stretched.Bounds.Height);
        var (ox, oy) = CornerOf(frame, Opposite(handle));
        Assert.Equal((ox, oy), CornerOf(stretched.Bounds, Opposite(handle)));

        // Each source pixel became a 2×2 block: red on the left half, blue on the right.
        Assert.All(Enumerable.Range(0, 16 * 8), i => Assert.Equal(i % 16 < 8 ? Red : Blue, stretched.FloatingPixels![i]));
    }

    private static FrameHandle Opposite(FrameHandle corner) => corner switch
    {
        FrameHandle.TopLeft => FrameHandle.BottomRight,
        FrameHandle.TopRight => FrameHandle.BottomLeft,
        FrameHandle.BottomRight => FrameHandle.TopLeft,
        _ => FrameHandle.TopRight,
    };

    [Fact]
    public void MoveSelectedPixels_ShiftPressedAndReleasedMidDrag_SwitchesFromTheSameDragStart()
    {
        var editor = CreateSelection();
        editor.Apply(new PointerInput(PointerAction.Down, 28, 24)); // the bottom-right handle

        Move(editor, 36, 25, shift: false);
        Assert.Equal(new IntRect(20, 20, 16, 5), editor.Selection!.Value.Bounds);

        Move(editor, 36, 25, shift: true);
        Assert.Equal(new IntRect(20, 20, 16, 8), editor.Selection!.Value.Bounds);

        Move(editor, 36, 25, shift: false);
        Assert.Equal(new IntRect(20, 20, 16, 5), editor.Selection!.Value.Bounds);

        Move(editor, 30, 32, shift: true); // height ×3 beats width ×1.25
        editor.Apply(new PointerInput(PointerAction.Up, 30, 32, Shift: true));
        Assert.Equal(new IntRect(20, 20, 24, 12), editor.Selection!.Value.Bounds);
    }

    [Fact]
    public void MoveSelectedPixels_ShiftEdgeDrag_StretchesOneWayAsBefore()
    {
        var editor = CreateSelection();

        editor.Apply(new PointerInput(PointerAction.Down, 28, 22)); // the right handle
        Move(editor, 36, 25, shift: true);
        editor.Apply(new PointerInput(PointerAction.Up, 36, 25, Shift: true));

        Assert.Equal(new IntRect(20, 20, 16, 4), editor.Selection!.Value.Bounds);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(30)]
    public void MoveSelectedPixels_AfterATurn_KeepsTheRatioOfTheTurnedFrame(double degrees)
    {
        var editor = CreateSelection();
        editor.Rotate(degrees); // the frame becomes the upright rectangle around the turned pixels
        var frame = editor.Selection!.Value.Bounds;

        editor.Apply(new PointerInput(PointerAction.Down, frame.Right, frame.Bottom));
        Move(editor, frame.Right + 1, frame.Bottom + frame.Height, shift: true); // height ×2 beats width
        editor.Apply(new PointerInput(PointerAction.Up, frame.Right + 1, frame.Bottom + frame.Height, Shift: true));

        Assert.Equal(new IntRect(frame.X, frame.Y, 2 * frame.Width, 2 * frame.Height), editor.Selection!.Value.Bounds);
    }

    // Pending shapes.

    private static DocumentEditor DrawShape(ShapeKind kind)
    {
        var editor = new DocumentEditor(Document.CreateBlank(128, 128))
        {
            ActiveTool = new ShapeTool(new ShapeSettings { Kind = kind, Fill = ShapeSettings.CanFill(kind) ? ShapeStyle.Solid : ShapeStyle.None }),
            PrimaryColor = Red,
            SecondaryColor = Blue,
            HandleTolerance = 1,
        };
        switch (kind)
        {
            case ShapeKind.Curve:
                Script.Stroke(editor, PointerButton.Left, (50.5, 70.5), (70.5, 70.5));
                Script.Stroke(editor, PointerButton.Left, (60.5, 60.5));
                Script.Stroke(editor, PointerButton.Left, (60.5, 60.5));
                break;
            case ShapeKind.Polygon:
                Script.Stroke(editor, PointerButton.Left, (50.5, 70.5), (70.5, 70.5));
                Script.Stroke(editor, PointerButton.Left, (60.5, 60.5));
                Script.Stroke(editor, PointerButton.Left, (51.5, 69.5)); // near the first corner: closes it
                break;
            default:
                Script.Stroke(editor, PointerButton.Left, (50.5, 60.5), (69.5, 69.5)); // 20×10 at (50, 60)
                break;
        }

        return editor;
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void PendingShape_ShiftCornerDrag_ScalesInProportion(ShapeKind kind)
    {
        var editor = DrawShape(kind);
        var frame = editor.Adornment!.Value.Frame;

        editor.Apply(new PointerInput(PointerAction.Down, frame.Right, frame.Bottom));
        Move(editor, frame.Right + frame.Width, frame.Bottom + 1, shift: true); // width ×2 beats height
        editor.Apply(new PointerInput(PointerAction.Up, frame.Right + frame.Width, frame.Bottom + 1, Shift: true));

        Assert.Equal(new IntRect(frame.X, frame.Y, 2 * frame.Width, 2 * frame.Height), editor.Adornment!.Value.Frame);
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void PendingShape_EachCorner_KeepsTheOppositeCornerFixed(FrameHandle handle)
    {
        var editor = DrawShape(ShapeKind.Rectangle);
        var frame = editor.Adornment!.Value.Frame; // 20×10 at (50, 60)
        var (x, y) = CornerOf(frame, handle);
        var (sx, sy) = (x == frame.X ? -1 : 1, y == frame.Y ? -1 : 1);

        editor.Apply(new PointerInput(PointerAction.Down, x, y));
        Move(editor, x + sx, y + sy * 10, shift: true); // height ×2 beats width
        editor.Apply(new PointerInput(PointerAction.Up, x + sx, y + sy * 10, Shift: true));
        editor.CommitPending();

        var expected = new IntRect(sx < 0 ? frame.Right - 40 : frame.X, sy < 0 ? frame.Bottom - 20 : frame.Y, 40, 20);
        var layer = editor.Document.ActiveLayer.Pixels;
        Assert.Equal(Red, layer.GetPixel(expected.X, expected.Y));
        Assert.Equal(Red, layer.GetPixel(expected.Right - 1, expected.Bottom - 1));
        Assert.Equal(Rgba.White, layer.GetPixel(expected.X - 1, expected.Y));
        Assert.Equal(Rgba.White, layer.GetPixel(expected.Right, expected.Bottom - 1));
    }

    [Fact]
    public void PendingShape_ShiftPressedAndReleasedMidDrag_SwitchesFromTheSameDragStart()
    {
        var editor = DrawShape(ShapeKind.Ellipse); // 20×10 at (50, 60)
        editor.Apply(new PointerInput(PointerAction.Down, 70, 70));

        Move(editor, 90, 71, shift: true);
        Assert.Equal(new IntRect(50, 60, 40, 20), editor.Adornment!.Value.Frame);

        Move(editor, 90, 71, shift: false);
        Assert.Equal(new IntRect(50, 60, 40, 11), editor.Adornment!.Value.Frame);

        Move(editor, 90, 71, shift: true);
        editor.Apply(new PointerInput(PointerAction.Up, 90, 71, Shift: true));
        Assert.Equal(new IntRect(50, 60, 40, 20), editor.Adornment!.Value.Frame);
    }

    [Fact]
    public void PendingShape_ShiftEdgeDrag_StretchesOneWayAsBefore()
    {
        var editor = DrawShape(ShapeKind.Rectangle);

        editor.Apply(new PointerInput(PointerAction.Down, 70, 65)); // the right handle
        Move(editor, 80, 75, shift: true);
        editor.Apply(new PointerInput(PointerAction.Up, 80, 75, Shift: true));

        Assert.Equal(new IntRect(50, 60, 30, 10), editor.Adornment!.Value.Frame);
    }
}
