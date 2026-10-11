namespace DipsyPaint.Engine;

/// <summary>A part of an <see cref="Adornment"/> that the pointer can grab.</summary>
public enum FrameHandle
{
    None,

    /// <summary>Inside the frame (on a pixel of <see cref="Adornment.Mask"/>, when it has one), away from the handles: dragging moves the content.</summary>
    Move,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,

    /// <summary>The rotate handle above the frame: dragging it turns the content (reference Paint's freeform rotate).</summary>
    Rotate,
}

/// <summary>
/// The frame and resize handles shown around pending content (see <see cref="Overlay"/>), in
/// document pixels: the frame runs along the edges of <paramref name="Frame"/>, with a handle at
/// each corner and edge midpoint, and a rotate handle above the top edge (<see cref="RotateHandle"/>).
/// The shell draws it at a constant on-screen size; tools hit-test pointer input against it and
/// apply handle drags with <see cref="Drag"/>. Without <see cref="HasHandles"/> it is the frame alone
/// (a selection made or moved as an outline, ADR-0025).
/// </summary>
public readonly record struct Adornment(IntRect Frame)
{
    /// <summary>Whether the frame has resize and rotate handles; true unless set otherwise.</summary>
    public bool HasHandles { get; init; } = true;

    /// <summary>
    /// Which pixels within the frame (row-major, <see cref="Frame"/>-sized) take a press that moves the content:
    /// a free-form or magic-wand selection's shape (issue #111). Null for the whole frame. Callers must not change it.
    /// </summary>
    public bool[]? Mask { get; init; }

    /// <summary>How far the rotate handle sits above the frame, in multiples of the handle tolerance (so constant on screen).</summary>
    public const double RotateHandleDistance = 4;

    private static readonly FrameHandle[] Order =
    [
        FrameHandle.TopLeft, FrameHandle.TopRight, FrameHandle.BottomRight, FrameHandle.BottomLeft,
        FrameHandle.Top, FrameHandle.Right, FrameHandle.Bottom, FrameHandle.Left,
    ];

    /// <summary>Each handle and its centre, corners first; none without <see cref="HasHandles"/>.</summary>
    public IEnumerable<(FrameHandle Handle, double X, double Y)> Handles
    {
        get
        {
            foreach (var handle in HasHandles ? Order : [])
            {
                var (x, y) = CentreOf(handle);
                yield return (handle, x, y);
            }
        }
    }

    /// <summary>
    /// The centre of the rotate handle, above the middle of the top edge by
    /// <see cref="RotateHandleDistance"/> × <paramref name="tolerance"/> document pixels.
    /// </summary>
    public (double X, double Y) RotateHandle(double tolerance) =>
        (Frame.X + Frame.Width / 2.0, Frame.Y - RotateHandleDistance * tolerance);

    /// <summary>
    /// The handle within <paramref name="tolerance"/> document pixels of the point (corners win, then
    /// edges, then the rotate handle; none without <see cref="HasHandles"/>), else <see cref="FrameHandle.Move"/> inside the frame
    /// (on a pixel of <see cref="Mask"/>, when it has one), else <see cref="FrameHandle.None"/>.
    /// </summary>
    public FrameHandle HitTest(double x, double y, double tolerance)
    {
        foreach (var (handle, hx, hy) in Handles)
        {
            if (Math.Abs(x - hx) <= tolerance && Math.Abs(y - hy) <= tolerance)
            {
                return handle;
            }
        }

        var (rx, ry) = RotateHandle(tolerance);
        if (HasHandles && Math.Abs(x - rx) <= tolerance && Math.Abs(y - ry) <= tolerance)
        {
            return FrameHandle.Rotate;
        }

        return x >= Frame.X && x < Frame.Right && y >= Frame.Y && y < Frame.Bottom
            && (Mask is null || Mask[((int)Math.Floor(y) - Frame.Y) * Frame.Width + (int)Math.Floor(x) - Frame.X])
            ? FrameHandle.Move
            : FrameHandle.None;
    }

    /// <summary>
    /// Applies a drag of <paramref name="handle"/> by whole pixels to a frame given by two opposite
    /// corner pixels (inclusive; for a line, its end points), as they were when the drag began.
    /// Only the coordinates on the dragged sides move, so dragging past the opposite side flips
    /// the frame; <see cref="FrameHandle.Move"/> moves both corners and <see cref="FrameHandle.Rotate"/>
    /// none (tools turn the content themselves, see <see cref="Tools.IRotatableTool"/>).
    /// With <paramref name="keepAspect"/> (Shift), a corner drag keeps the frame's width-to-height ratio
    /// (see <see cref="DragCornerKeepingAspect"/>); other handles ignore it.
    /// </summary>
    public static ((int X, int Y) A, (int X, int Y) B) Drag(
        (int X, int Y) a, (int X, int Y) b, FrameHandle handle, int dx, int dy, bool keepAspect = false)
    {
        if (handle == FrameHandle.Move)
        {
            return ((a.X + dx, a.Y + dy), (b.X + dx, b.Y + dy));
        }

        if (keepAspect && handle is FrameHandle.TopLeft or FrameHandle.TopRight or FrameHandle.BottomRight or FrameHandle.BottomLeft)
        {
            return DragCornerKeepingAspect(a, b, handle, dx, dy);
        }

        var aIsLeft = a.X <= b.X;
        var aIsTop = a.Y <= b.Y;
        if (handle is FrameHandle.TopLeft or FrameHandle.Left or FrameHandle.BottomLeft)
        {
            if (aIsLeft) { a.X += dx; } else { b.X += dx; }
        }

        if (handle is FrameHandle.TopRight or FrameHandle.Right or FrameHandle.BottomRight)
        {
            if (aIsLeft) { b.X += dx; } else { a.X += dx; }
        }

        if (handle is FrameHandle.TopLeft or FrameHandle.Top or FrameHandle.TopRight)
        {
            if (aIsTop) { a.Y += dy; } else { b.Y += dy; }
        }

        if (handle is FrameHandle.BottomLeft or FrameHandle.Bottom or FrameHandle.BottomRight)
        {
            if (aIsTop) { b.Y += dy; } else { a.Y += dy; }
        }

        return (a, b);
    }

    /// <summary>
    /// A corner drag that keeps the ratio of the frame's width to its height (in pixels) as it was when
    /// the drag began; the opposite corner stays fixed. The frame takes the larger of the two proportional
    /// changes the pointer makes, so the dragged corner follows the pointer along one axis. On each axis,
    /// a corner dragged past the opposite one flips the frame, as an unconstrained drag does.
    /// </summary>
    private static ((int X, int Y) A, (int X, int Y) B) DragCornerKeepingAspect(
        (int X, int Y) a, (int X, int Y) b, FrameHandle handle, int dx, int dy)
    {
        var left = handle is FrameHandle.TopLeft or FrameHandle.BottomLeft;
        var top = handle is FrameHandle.TopLeft or FrameHandle.TopRight;

        // Whether a holds the dragged corner's x (and y); the other point holds the fixed corner's.
        var aMovesX = left == (a.X <= b.X);
        var aMovesY = top == (a.Y <= b.Y);
        var (fixedX, startX) = aMovesX ? (b.X, a.X) : (a.X, b.X);
        var (fixedY, startY) = aMovesY ? (b.Y, a.Y) : (a.Y, b.Y);

        // Spans from the fixed corner, signed, before and after the drag.
        int spanX = startX - fixedX, spanY = startY - fixedY;
        int toX = spanX + dx, toY = spanY + dy;
        double width = Math.Abs(spanX) + 1, height = Math.Abs(spanY) + 1;
        var scale = Math.Max((Math.Abs(toX) + 1) / width, (Math.Abs(toY) + 1) / height);

        // Each axis faces the way the pointer is from the fixed corner; level with it, the way it faced.
        int Extent(double size, int to, bool towardsLow) =>
            ((int)Math.Round(size * scale, MidpointRounding.AwayFromZero) - 1) * (to != 0 ? Math.Sign(to) : towardsLow ? -1 : 1);

        var x = fixedX + Extent(width, toX, spanX != 0 ? spanX < 0 : left);
        var y = fixedY + Extent(height, toY, spanY != 0 ? spanY < 0 : top);
        return ((aMovesX ? x : a.X, aMovesY ? y : a.Y), (aMovesX ? b.X : x, aMovesY ? b.Y : y));
    }

    /// <summary>The rectangle of pixels spanned by two corner pixels (inclusive), in either order.</summary>
    public static IntRect FrameOf((int X, int Y) a, (int X, int Y) b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

    private (double X, double Y) CentreOf(FrameHandle handle)
    {
        double left = Frame.X, right = Frame.Right, top = Frame.Y, bottom = Frame.Bottom;
        double midX = left + Frame.Width / 2.0, midY = top + Frame.Height / 2.0;
        return handle switch
        {
            FrameHandle.TopLeft => (left, top),
            FrameHandle.Top => (midX, top),
            FrameHandle.TopRight => (right, top),
            FrameHandle.Right => (right, midY),
            FrameHandle.BottomRight => (right, bottom),
            FrameHandle.Bottom => (midX, bottom),
            FrameHandle.BottomLeft => (left, bottom),
            _ => (left, midY),
        };
    }
}
