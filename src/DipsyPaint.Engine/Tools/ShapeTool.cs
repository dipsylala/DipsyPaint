namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Draws lines, rectangles and ellipses as in reference Paint. Dragging draws the shape (Shift
/// keeps it square, circular or at a multiple of 45°); it then stays pending in the overlay, where
/// dragging a handle resizes it and dragging inside moves it, until a press outside it commits it
/// (and starts the next shape). The left button outlines in the primary colour and fills in the
/// secondary; the right button swaps them.
/// </summary>
public sealed class ShapeTool(ShapeSettings settings) : IPendingTool
{
    private enum Gesture
    {
        None,
        Draw,
        Adjust,
    }

    private bool _pending;
    private ShapeKind _kind;
    private bool _swapColors;

    // The frame corners (rectangle, ellipse) or end points (line), in pixels, inclusive.
    private (int X, int Y) _a;
    private (int X, int Y) _b;

    private Gesture _gesture;
    private FrameHandle _handle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _dragA;
    private (int X, int Y) _dragB;

    public Adornment? Adornment => _pending && _gesture != Gesture.Draw ? new Adornment(Engine.Adornment.FrameOf(_a, _b)) : null;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var pixel = (X: (int)Math.Floor(input.X), Y: (int)Math.Floor(input.Y));
        var dirty = IntRect.Empty;
        if (input.Action == PointerAction.Down)
        {
            var hit = Adornment?.HitTest(input.X, input.Y, context.HandleTolerance) ?? FrameHandle.None;
            if (hit != FrameHandle.None)
            {
                (_gesture, _handle, _dragStart, _dragA, _dragB) = (Gesture.Adjust, hit, pixel, _a, _b);
                return IntRect.Empty;
            }

            dirty = Commit(context);
            (_gesture, _pending, _kind, _swapColors, _a, _b) =
                (Gesture.Draw, true, settings.Kind, input.Button == PointerButton.Right, pixel, pixel);
            return dirty;
        }

        switch (_gesture)
        {
            case Gesture.Draw:
                _b = input.Shift ? Constrain(_a, pixel) : pixel;
                break;
            case Gesture.Adjust:
                (_a, _b) = Engine.Adornment.Drag(_dragA, _dragB, _handle, pixel.X - _dragStart.X, pixel.Y - _dragStart.Y);
                break;
            default:
                return IntRect.Empty;
        }

        if (input.Action == PointerAction.Up)
        {
            var drewNothing = _gesture == Gesture.Draw && _a == _b; // a click draws no shape
            _gesture = Gesture.None;
            if (drewNothing)
            {
                return Cancel(context);
            }
        }

        return dirty.Union(Render(context));
    }

    public IntRect Commit(ToolContext context)
    {
        if (!_pending)
        {
            return IntRect.Empty;
        }

        var dirty = context.Overlay.DrawOnto(context.Target);
        return dirty.Union(Cancel(context));
    }

    public IntRect Cancel(ToolContext context)
    {
        if (!_pending)
        {
            return IntRect.Empty;
        }

        _pending = false;
        _gesture = Gesture.None;
        return context.Overlay.Clear();
    }

    public IntRect Refresh(ToolContext context) => _pending && (_gesture != Gesture.Draw || _a != _b) ? Render(context) : IntRect.Empty;

    private IntRect Render(ToolContext context)
    {
        var (outline, fill) = _swapColors
            ? (context.SecondaryColor, context.PrimaryColor)
            : (context.PrimaryColor, context.SecondaryColor);
        var (bounds, pixels) = ShapeRasterizer.Render(_kind, _a, _b, settings.Outline, settings.Fill, settings.Size, outline, fill);
        return context.Overlay.Show(bounds, pixels);
    }

    /// <summary>Square or circle for a box; horizontal, vertical or 45° for a line.</summary>
    private (int X, int Y) Constrain((int X, int Y) from, (int X, int Y) to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (_kind == ShapeKind.Line)
        {
            const double tan22_5 = 0.41421356;
            if (Math.Abs(dy) <= Math.Abs(dx) * tan22_5)
            {
                return (to.X, from.Y);
            }

            if (Math.Abs(dx) <= Math.Abs(dy) * tan22_5)
            {
                return (from.X, to.Y);
            }
        }

        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return (from.X + (dx < 0 ? -side : side), from.Y + (dy < 0 ? -side : side));
    }
}
