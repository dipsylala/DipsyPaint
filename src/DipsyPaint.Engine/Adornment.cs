namespace DipsyPaint.Engine;

/// <summary>A part of an <see cref="Adornment"/> that the pointer can grab.</summary>
public enum FrameHandle
{
    None,

    /// <summary>Inside the frame, away from the handles: dragging moves the content.</summary>
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
/// apply handle drags with <see cref="Drag"/>.
/// </summary>
public readonly record struct Adornment(IntRect Frame)
{
    /// <summary>How far the rotate handle sits above the frame, in multiples of the handle tolerance (so constant on screen).</summary>
    public const double RotateHandleDistance = 4;

    private static readonly FrameHandle[] Order =
    [
        FrameHandle.TopLeft, FrameHandle.TopRight, FrameHandle.BottomRight, FrameHandle.BottomLeft,
        FrameHandle.Top, FrameHandle.Right, FrameHandle.Bottom, FrameHandle.Left,
    ];

    /// <summary>Each handle and its centre, corners first.</summary>
    public IEnumerable<(FrameHandle Handle, double X, double Y)> Handles
    {
        get
        {
            foreach (var handle in Order)
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
    /// edges, then the rotate handle), else <see cref="FrameHandle.Move"/> inside the frame, else
    /// <see cref="FrameHandle.None"/>.
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
        if (Math.Abs(x - rx) <= tolerance && Math.Abs(y - ry) <= tolerance)
        {
            return FrameHandle.Rotate;
        }

        return x >= Frame.X && x < Frame.Right && y >= Frame.Y && y < Frame.Bottom ? FrameHandle.Move : FrameHandle.None;
    }

    /// <summary>
    /// Applies a drag of <paramref name="handle"/> by whole pixels to a frame given by two opposite
    /// corner pixels (inclusive; for a line, its end points), as they were when the drag began.
    /// Only the coordinates on the dragged sides move, so dragging past the opposite side flips
    /// the frame; <see cref="FrameHandle.Move"/> moves both corners and <see cref="FrameHandle.Rotate"/>
    /// none (tools turn the content themselves, see <see cref="Tools.IRotatableTool"/>).
    /// </summary>
    public static ((int X, int Y) A, (int X, int Y) B) Drag(
        (int X, int Y) a, (int X, int Y) b, FrameHandle handle, int dx, int dy)
    {
        if (handle == FrameHandle.Move)
        {
            return ((a.X + dx, a.Y + dy), (b.X + dx, b.Y + dy));
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
