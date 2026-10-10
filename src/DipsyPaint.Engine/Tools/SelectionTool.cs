namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's rectangle selection. Dragging draws a marquee, kept within the canvas.
/// Dragging inside it moves the selected pixels and dragging a handle stretches them (nearest
/// neighbour); the first such drag lifts them off the layer, leaving it transparent there, as the
/// eraser does. The selection stays pending (see <see cref="IPendingTool"/>) until committed, which
/// composites lifted pixels source-over at their new place and removes the selection; a marquee
/// that was never lifted writes nothing. <see cref="Delete"/> removes the selected pixels; <see cref="Replace"/>
/// rotates, flips or resizes them; <see cref="Paste"/> shows pasted pixels as a lifted selection. The right
/// button does nothing (Paint opens a context menu there).
/// </summary>
public sealed class SelectionTool : IPendingTool
{
    private enum Gesture
    {
        None,
        Marquee,
        Adjust,
    }

    private bool _selected;

    // The frame's corner pixels, inclusive, as for shapes (see Adornment.Drag).
    private (int X, int Y) _a;
    private (int X, int Y) _b;

    // The pixels as lifted, and as shown (stretched to the frame); null until lifted.
    private Rgba[]? _lifted;
    private IntRect _liftedBounds;
    private Rgba[]? _shown;

    private Gesture _gesture;
    private FrameHandle _handle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _dragA;
    private (int X, int Y) _dragB;

    public Adornment? Adornment => _selected ? new Adornment(Bounds) : null;

    /// <summary>The current selection, or null when there is none.</summary>
    public Selection? Selection => _selected ? new Selection(Bounds, _shown) : null;

    private IntRect Bounds => Engine.Adornment.FrameOf(_a, _b);

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var pixel = (X: (int)Math.Floor(input.X), Y: (int)Math.Floor(input.Y));
        if (input.Action == PointerAction.Down)
        {
            if (input.Button != PointerButton.Left)
            {
                _gesture = Gesture.None;
                return IntRect.Empty;
            }

            var hit = Adornment?.HitTest(input.X, input.Y, context.HandleTolerance) ?? FrameHandle.None;
            if (hit != FrameHandle.None)
            {
                (_gesture, _handle, _dragStart, _dragA, _dragB) = (Gesture.Adjust, hit, pixel, _a, _b);
                return IntRect.Empty;
            }

            // A press outside: the editor has committed the previous selection; draw a new one.
            var corner = Clamp(context.Target, pixel);
            (_gesture, _selected, _a, _b) = (Gesture.Marquee, true, corner, corner);
            return IntRect.Empty;
        }

        var dirty = IntRect.Empty;
        switch (_gesture)
        {
            case Gesture.Marquee:
                _b = Clamp(context.Target, pixel);
                break;
            case Gesture.Adjust:
                var (a, b) = Engine.Adornment.Drag(_dragA, _dragB, _handle, pixel.X - _dragStart.X, pixel.Y - _dragStart.Y);
                if ((a, b) != (_a, _b))
                {
                    dirty = Lift(context);
                    (_a, _b) = (a, b);
                    dirty = dirty.Union(Show(context));
                }

                break;
            default:
                return IntRect.Empty;
        }

        if (input.Action == PointerAction.Up)
        {
            _selected = _gesture == Gesture.Adjust || _a != _b; // a click selects nothing
            _gesture = Gesture.None;
        }

        return dirty;
    }

    public IntRect Commit(ToolContext context)
    {
        var dirty = _lifted is null ? IntRect.Empty : context.Overlay.DrawOnto(context.Target);
        return dirty.Union(Cancel(context));
    }

    public IntRect Cancel(ToolContext context)
    {
        (_selected, _gesture, _lifted, _shown) = (false, Gesture.None, null, null);
        return context.Overlay.Clear();
    }

    public IntRect Refresh(ToolContext context) => IntRect.Empty;

    /// <summary>
    /// Removes the selected pixels: clears them on the layer if they were never lifted (the hole of
    /// lifted ones is already there) and discards the selection. Returns the changed rectangle.
    /// </summary>
    public IntRect Delete(ToolContext context)
    {
        if (Selection is not { } selection)
        {
            return IntRect.Empty;
        }

        var dirty = IntRect.Empty;
        if (selection.FloatingPixels is null)
        {
            context.Target.WriteRegion(selection.Bounds, new Rgba[selection.Bounds.Width * selection.Bounds.Height]);
            dirty = selection.Bounds;
        }

        return dirty.Union(Cancel(context));
    }

    /// <summary>
    /// Replaces the selected pixels (lifting them first) with <paramref name="map"/>'s result, a new
    /// <paramref name="width"/>×<paramref name="height"/> bitmap computed from the shown pixels and
    /// their size; the frame keeps its top-left corner. Used for rotating, flipping and resizing a
    /// selection. Returns the changed rectangle; empty when nothing is selected.
    /// </summary>
    public IntRect Replace(ToolContext context, int width, int height, Func<Rgba[], int, int, Rgba[]> map)
    {
        if (!_selected)
        {
            return IntRect.Empty;
        }

        var dirty = Lift(context);
        var bounds = Bounds;
        _lifted = map(_shown ?? _lifted!, bounds.Width, bounds.Height);
        _liftedBounds = new IntRect(bounds.X, bounds.Y, width, height);
        (_a, _b) = ((bounds.X, bounds.Y), (bounds.X + width - 1, bounds.Y + height - 1));
        return dirty.Union(Show(context));
    }

    /// <summary>
    /// Shows <paramref name="image"/> as a selection already lifted, its top-left at (<paramref name="x"/>,
    /// <paramref name="y"/>), as pasted content arrives in reference Paint: nothing is written to the
    /// layer (no hole), it can be moved and stretched, and committing composites it there. Replaces any
    /// current selection without committing it. The tool keeps <see cref="PixelImage.Pixels"/>, so the
    /// caller must not change them. Returns the changed rectangle.
    /// </summary>
    public IntRect Paste(ToolContext context, PixelImage image, int x, int y)
    {
        (_selected, _gesture, _a, _b) = (true, Gesture.None, (x, y), (x + image.Width - 1, y + image.Height - 1));
        (_lifted, _liftedBounds) = (image.Pixels, Bounds);
        return Show(context);
    }

    // Takes the selected pixels off the layer, leaving it transparent there, the first time only.
    // Returns the layer's changed rectangle.
    private IntRect Lift(ToolContext context)
    {
        if (_lifted is not null)
        {
            return IntRect.Empty;
        }

        _liftedBounds = Bounds;
        _lifted = new Rgba[_liftedBounds.Width * _liftedBounds.Height];
        context.Target.ReadRegion(_liftedBounds, _lifted);
        context.Target.WriteRegion(_liftedBounds, new Rgba[_lifted.Length]);
        return _liftedBounds;
    }

    private IntRect Show(ToolContext context)
    {
        var bounds = Bounds;
        _shown = bounds.Width == _liftedBounds.Width && bounds.Height == _liftedBounds.Height
            ? _lifted!
            : Stretch(_lifted!, _liftedBounds.Width, _liftedBounds.Height, bounds.Width, bounds.Height);
        return context.Overlay.Show(bounds, _shown);
    }

    private static Rgba[] Stretch(Rgba[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        var result = new Rgba[width * height];
        for (var y = 0; y < height; y++)
        {
            var row = (int)((long)y * sourceHeight / height) * sourceWidth;
            for (var x = 0; x < width; x++)
            {
                result[y * width + x] = source[row + (int)((long)x * sourceWidth / width)];
            }
        }

        return result;
    }

    private static (int X, int Y) Clamp(IPixelAccess target, (int X, int Y) pixel) =>
        (Math.Clamp(pixel.X, 0, target.Width - 1), Math.Clamp(pixel.Y, 0, target.Height - 1));
}
