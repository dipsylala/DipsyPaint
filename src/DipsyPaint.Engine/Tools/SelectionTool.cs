namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's selection tool. Dragging marks out a new selection, kept within the canvas: a
/// rectangle, or with <see cref="SelectionShape.FreeForm"/> the area the pointer's path encloses
/// (<see cref="FreeFormPath"/> while dragging), or with <see cref="SelectionShape.MagicWand"/> the area of
/// similar colour around the pressed pixel (<see cref="MagicWand"/>, see also <see cref="PassesThrough"/>),
/// framed by its bounding rectangle. Dragging inside the
/// frame moves the selected pixels and dragging a handle stretches them (nearest neighbour); the first
/// such drag lifts them off the layer, leaving it transparent there, as the eraser does. Pixels outside
/// the selection's shape (<see cref="Selection.Mask"/>) stay on the layer, and so do pixels of the
/// secondary colour with <see cref="SelectionSettings.Transparent"/>. The selection stays pending (see
/// <see cref="IPendingTool"/>) until committed, which composites lifted pixels source-over at their new
/// place and removes the selection; a selection that was never lifted writes nothing. <see cref="Delete"/>
/// removes the selected pixels; <see cref="Replace"/> rotates, flips or resizes them; <see cref="Paste"/>
/// shows pasted pixels as a lifted selection; <see cref="Select"/> selects a given shape (select all,
/// invert). The right button does nothing (Paint opens a context menu there).
/// </summary>
public sealed class SelectionTool : IPendingTool
{
    private enum Gesture
    {
        None,
        Marquee,
        FreeForm,
        Adjust,
    }

    private readonly SelectionSettings _settings;
    private readonly List<(int X, int Y)> _path = [];

    private bool _selected;

    // The frame's corner pixels, inclusive, as for shapes (see Adornment.Drag).
    private (int X, int Y) _a;
    private (int X, int Y) _b;

    // Where the selection was made (or last replaced); the lifted pixels and the mask are this size.
    private IntRect _source;
    private bool[]? _mask;

    // The pixels as lifted (null until lifted), and the pixels and mask as shown (stretched to the frame).
    private Rgba[]? _lifted;
    private Rgba[]? _shown;
    private bool[]? _shownMask;

    private Gesture _gesture;
    private FrameHandle _handle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _dragA;
    private (int X, int Y) _dragB;

    public SelectionTool() : this(new SelectionSettings())
    {
    }

    public SelectionTool(SelectionSettings settings) => _settings = settings;

    public Adornment? Adornment => _selected ? new Adornment(Bounds) : null;

    /// <summary>The current selection, or null when there is none.</summary>
    public Selection? Selection => _selected ? new Selection(Bounds, _shown, _shownMask) : null;

    /// <summary>
    /// While a free-form selection is being dragged, the pixels the pointer has passed through (kept
    /// within the canvas), for the shell to draw; else null.
    /// </summary>
    public IReadOnlyList<(int X, int Y)>? FreeFormPath => _gesture == Gesture.FreeForm ? _path : null;

    private IntRect Bounds => Engine.Adornment.FrameOf(_a, _b);

    /// <summary>
    /// Whether a press at (<paramref name="x"/>, <paramref name="y"/>) within the selection's frame misses
    /// it: with <see cref="SelectionShape.MagicWand"/>, only the selected pixels and the handles take a
    /// press, so a click on an unselected pixel makes a new selection (the editor commits this one first).
    /// </summary>
    public bool PassesThrough(double x, double y, double tolerance) =>
        _settings.Shape == SelectionShape.MagicWand
        && Selection is { } selection
        && Adornment?.HitTest(x, y, tolerance) == FrameHandle.Move
        && !selection.Contains((int)Math.Floor(x), (int)Math.Floor(y));

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

            // A press outside: the editor has committed the previous selection; make a new one.
            var corner = Clamp(context.Target, pixel);
            (_lifted, _shown, _mask, _shownMask) = (null, null, null, null);
            if (_settings.Shape == SelectionShape.MagicWand)
            {
                (_gesture, _selected) = (Gesture.None, false);
                if (MagicWand.Select(context.Target, pixel.X, pixel.Y, _settings.Tolerance) is { } region)
                {
                    SetShape(region.Bounds, region.Mask);
                }
            }
            else if (_settings.Shape == SelectionShape.FreeForm)
            {
                (_gesture, _selected) = (Gesture.FreeForm, false);
                _path.Clear();
                _path.Add(corner);
            }
            else
            {
                (_gesture, _selected, _a, _b) = (Gesture.Marquee, true, corner, corner);
            }

            return IntRect.Empty;
        }

        var dirty = IntRect.Empty;
        switch (_gesture)
        {
            case Gesture.Marquee:
                _b = Clamp(context.Target, pixel);
                break;
            case Gesture.FreeForm:
                var point = Clamp(context.Target, pixel);
                if (point != _path[^1])
                {
                    _path.Add(point);
                }

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
            switch (_gesture)
            {
                case Gesture.Marquee:
                    _selected = _a != _b; // a click selects nothing
                    _source = Bounds;
                    break;
                case Gesture.FreeForm when SelectionMasks.FromPath(_path) is { } shape:
                    SetShape(shape.Bounds, shape.Mask);
                    break;
            }

            _gesture = Gesture.None;
            _path.Clear();
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
        (_selected, _gesture, _lifted, _shown, _mask, _shownMask) = (false, Gesture.None, null, null, null, null);
        _path.Clear();
        return context.Overlay.Clear();
    }

    /// <summary>Re-shows lifted pixels after the secondary colour or <see cref="SelectionSettings.Transparent"/> changed.</summary>
    public IntRect Refresh(ToolContext context) => _selected && _lifted is not null ? Show(context) : IntRect.Empty;

    /// <summary>
    /// Selects <paramref name="bounds"/> (within the canvas), or the pixels set in <paramref name="mask"/>
    /// (row-major, <paramref name="bounds"/>-sized; null for all) within it, as a selection not yet lifted, replacing
    /// any current one without committing it (the editor commits first). The tool keeps the mask, so the
    /// caller must not change it. Returns the changed rectangle.
    /// </summary>
    public IntRect Select(ToolContext context, IntRect bounds, bool[]? mask = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bounds.Width, 1, nameof(bounds));
        ArgumentOutOfRangeException.ThrowIfLessThan(bounds.Height, 1, nameof(bounds));
        if (mask is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(mask.Length, bounds.Width * bounds.Height, nameof(mask));
        }

        var dirty = Cancel(context);
        SetShape(bounds, mask);
        return dirty;
    }

    /// <summary>
    /// The selected pixels as they would be placed: the lifted pixels as shown, else the layer's within
    /// the selection; transparent outside its shape and, with <see cref="SelectionSettings.Transparent"/>,
    /// where they are the secondary colour. A new array; null when nothing is selected.
    /// </summary>
    public PixelImage? Copy(ToolContext context)
    {
        if (Selection is not { } selection)
        {
            return null;
        }

        var bounds = selection.Bounds;
        if (selection.FloatingPixels is { } floating)
        {
            return new PixelImage(bounds.Width, bounds.Height, (Rgba[])floating.Clone());
        }

        var pixels = new Rgba[bounds.Width * bounds.Height];
        context.Target.ReadRegion(bounds, pixels);
        for (var i = 0; i < pixels.Length; i++)
        {
            if (!Keeps(context, i, pixels[i]))
            {
                pixels[i] = Rgba.Transparent;
            }
        }

        return new PixelImage(bounds.Width, bounds.Height, pixels);
    }

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
            var bounds = selection.Bounds;
            var pixels = new Rgba[bounds.Width * bounds.Height];
            if (selection.Mask is { } mask)
            {
                context.Target.ReadRegion(bounds, pixels);
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = mask[i] ? Rgba.Transparent : pixels[i];
                }
            }

            context.Target.WriteRegion(bounds, pixels);
            dirty = bounds;
        }

        return dirty.Union(Cancel(context));
    }

    /// <summary>
    /// Replaces the selected pixels (lifting them first) with <paramref name="map"/>'s result, a new
    /// <paramref name="width"/>×<paramref name="height"/> bitmap computed from the shown pixels and
    /// their size; the frame keeps its top-left corner. The selection's shape is mapped the same way.
    /// Used for rotating, flipping and resizing a selection. Returns the changed rectangle; empty when
    /// nothing is selected.
    /// </summary>
    public IntRect Replace(ToolContext context, int width, int height, Func<Rgba[], int, int, Rgba[]> map)
    {
        if (!_selected)
        {
            return IntRect.Empty;
        }

        var dirty = Lift(context);
        var bounds = Bounds;
        var resized = (bounds.Width, bounds.Height) != (_source.Width, _source.Height);
        _lifted = map(resized ? Stretch(_lifted!, _source, bounds) : _lifted!, bounds.Width, bounds.Height);
        if (_mask is not null)
        {
            // The shape goes through the same map as opaque and transparent pixels.
            var shape = resized ? Stretch(_mask, _source, bounds) : _mask;
            var mapped = map(Array.ConvertAll(shape, selected => selected ? Rgba.White : Rgba.Transparent), bounds.Width, bounds.Height);
            _mask = Array.ConvertAll(mapped, pixel => pixel.A >= 128);
        }

        (_a, _b) = ((bounds.X, bounds.Y), (bounds.X + width - 1, bounds.Y + height - 1));
        _source = Bounds;
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
        (_lifted, _source, _mask) = (image.Pixels, Bounds, null);
        return Show(context);
    }

    private void SetShape(IntRect bounds, bool[]? mask)
    {
        (_selected, _a, _b) = (true, (bounds.X, bounds.Y), (bounds.Right - 1, bounds.Bottom - 1));
        (_source, _mask, _shownMask) = (bounds, mask, mask);
    }

    // Whether the selection takes the pixel at index i of the source rectangle, which has this colour.
    private bool Keeps(ToolContext context, int i, Rgba pixel) =>
        (_mask is null || _mask[i]) && !(_settings.Transparent && pixel == context.SecondaryColor);

    // Takes the selected pixels off the layer, leaving it transparent there, the first time only.
    // Returns the layer's changed rectangle.
    private IntRect Lift(ToolContext context)
    {
        if (_lifted is not null)
        {
            return IntRect.Empty;
        }

        var layer = new Rgba[_source.Width * _source.Height];
        context.Target.ReadRegion(_source, layer);
        _lifted = new Rgba[layer.Length];
        for (var i = 0; i < layer.Length; i++)
        {
            if (Keeps(context, i, layer[i]))
            {
                (_lifted[i], layer[i]) = (layer[i], Rgba.Transparent);
            }
        }

        context.Target.WriteRegion(_source, layer);
        return _source;
    }

    private IntRect Show(ToolContext context)
    {
        var bounds = Bounds;
        var resized = (bounds.Width, bounds.Height) != (_source.Width, _source.Height);
        _shown = resized ? Stretch(_lifted!, _source, bounds) : _lifted!;
        _shownMask = resized && _mask is not null ? Stretch(_mask, _source, bounds) : _mask;
        if (_settings.Transparent && Array.IndexOf(_shown, context.SecondaryColor) >= 0)
        {
            _shown = Array.ConvertAll(_shown, pixel => pixel == context.SecondaryColor ? Rgba.Transparent : pixel);
        }

        return context.Overlay.Show(bounds, _shown);
    }

    private static T[] Stretch<T>(T[] source, IntRect from, IntRect to)
    {
        var result = new T[to.Width * to.Height];
        for (var y = 0; y < to.Height; y++)
        {
            var row = (int)((long)y * from.Height / to.Height) * from.Width;
            for (var x = 0; x < to.Width; x++)
            {
                result[y * to.Width + x] = source[row + (int)((long)x * from.Width / to.Width)];
            }
        }

        return result;
    }

    private static (int X, int Y) Clamp(IPixelAccess target, (int X, int Y) pixel) =>
        (Math.Clamp(pixel.X, 0, target.Width - 1), Math.Clamp(pixel.Y, 0, target.Height - 1));
}
