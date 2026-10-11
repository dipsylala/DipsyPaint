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
/// invert). Dragging the rotate handle, or <see cref="Rotate"/>, turns the selected pixels and their
/// shape to any angle about the frame's centre (bilinear; while the handle is dragged, the overlay shows
/// a quick nearest-neighbour preview); the frame grows to the upright rectangle around them,
/// transparent where nothing lands. The right button does nothing (Paint opens a context
/// menu there).
/// </summary>
public sealed class SelectionTool : IRotatableTool
{
    private enum Gesture
    {
        None,
        Marquee,
        FreeForm,
        Adjust,
        Rotate,
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

    // The turn so far: the pixels and shape (null for all) as they were before the first turn, their
    // size, the angle since and the centre turned about. Every turn is computed from these, so turning
    // again does not blur further. Moving keeps them; any other change starts afresh.
    private Rgba[]? _unturned;
    private bool[]? _unturnedMask;
    private (int Width, int Height) _unturnedSize;
    private double _angle;
    private (double X, double Y) _turnCentre;
    private RotateDrag? _rotateDrag;

    // While the rotate handle is dragged: the angle and frame shown. The overlay shows a quick preview of
    // the turn (Overlay.ShowTurned) and the selection stays as it was at the press; releasing the handle,
    // or committing, turns it exactly (bilinear). Null otherwise.
    private (double Angle, IntRect Frame)? _preview;

    private Gesture _gesture;
    private FrameHandle _handle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _dragA;
    private (int X, int Y) _dragB;

    public SelectionTool() : this(new SelectionSettings())
    {
    }

    public SelectionTool(SelectionSettings settings) => _settings = settings;

    public Adornment? Adornment => _selected ? new Adornment(_preview?.Frame ?? Bounds) : null;

    /// <summary>
    /// The current selection, or null when there is none. While the rotate handle is dragged it stays as it
    /// was at the press (the overlay shows a preview); it is turned when the handle is released.
    /// </summary>
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
            if (hit == FrameHandle.Rotate)
            {
                var (centreX, centreY) = TurnCentre;
                (_gesture, _rotateDrag) = (Gesture.Rotate, new RotateDrag(centreX, centreY, input, Angle));
                return IntRect.Empty;
            }

            if (hit != FrameHandle.None)
            {
                (_gesture, _handle, _dragStart, _dragA, _dragB) = (Gesture.Adjust, hit, pixel, _a, _b);
                return IntRect.Empty;
            }

            // A press outside: the editor has committed the previous selection; make a new one.
            var corner = Clamp(context.Target, pixel);
            (_lifted, _shown, _mask, _shownMask, _unturned) = (null, null, null, null, null);
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
                    dirty = Settle(context).Union(Lift(context));
                    if (_handle == FrameHandle.Move)
                    {
                        _turnCentre = (_turnCentre.X + a.X - _a.X, _turnCentre.Y + a.Y - _a.Y);
                    }
                    else
                    {
                        _unturned = null; // stretched
                    }

                    (_a, _b) = (a, b);
                    dirty = dirty.Union(Show(context));
                }

                break;
            case Gesture.Rotate:
                var angle = _rotateDrag!.AngleAt(input);
                var released = input.Action == PointerAction.Up;
                if (angle != (_preview?.Angle ?? Angle) || (released && _preview is not null))
                {
                    dirty = Turn(context, angle, _rotateDrag.CentreX, _rotateDrag.CentreY, preview: !released);
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

            (_gesture, _rotateDrag) = (Gesture.None, null);
            _path.Clear();
        }

        return dirty;
    }

    public IntRect Commit(ToolContext context)
    {
        var dirty = Settle(context);
        dirty = dirty.Union(_lifted is null ? IntRect.Empty : context.Overlay.DrawOnto(context.Target));
        return dirty.Union(Cancel(context));
    }

    public IntRect Cancel(ToolContext context)
    {
        (_selected, _gesture, _lifted, _shown, _mask, _shownMask) = (false, Gesture.None, null, null, null, null);
        (_unturned, _rotateDrag, _preview) = (null, null, null);
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
        Settle(context);
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

        var dirty = Settle(context).Union(Lift(context));
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
        (_source, _unturned) = (Bounds, null);
        return dirty.Union(Show(context));
    }

    /// <summary>
    /// Turns the selected pixels (lifting them first) and the selection's shape <paramref name="degrees"/>
    /// clockwise about the frame's centre, sampling bilinearly; the frame becomes the upright rectangle
    /// around them, transparent where nothing lands. Successive turns are computed from the pixels as they
    /// were before the first, so they do not blur further, until the selection is stretched or replaced.
    /// Returns the changed rectangle; empty when nothing is selected.
    /// </summary>
    public IntRect Rotate(ToolContext context, double degrees)
    {
        if (!_selected)
        {
            return IntRect.Empty;
        }

        var (centreX, centreY) = TurnCentre;
        return Turn(context, Math.IEEERemainder(Angle + degrees, 360), centreX, centreY);
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
        (_lifted, _source, _mask, _unturned) = (image.Pixels, Bounds, null, null);
        return Show(context);
    }

    // The angle turned so far, and the centre to turn about.
    private double Angle => _unturned is null ? 0 : _angle;

    private (double X, double Y) TurnCentre =>
        _unturned is null ? (Bounds.X + Bounds.Width / 2.0, Bounds.Y + Bounds.Height / 2.0) : _turnCentre;

    // Shows the pixels as they were before the first turn, turned to `angle` about the centre: exactly
    // (bilinear), or with `preview` as a quick preview in the overlay only (see _preview).
    private IntRect Turn(ToolContext context, double angle, double centreX, double centreY, bool preview = false)
    {
        var dirty = Lift(context);
        if (_unturned is null)
        {
            var bounds = Bounds;
            var resized = (bounds.Width, bounds.Height) != (_source.Width, _source.Height);
            _unturned = resized ? Stretch(_lifted!, _source, bounds) : _lifted!;
            _unturnedMask = _mask is not null && resized ? Stretch(_mask, _source, bounds) : _mask;
            _unturnedSize = (bounds.Width, bounds.Height);
            (_angle, _turnCentre) = (0, (centreX, centreY));
        }

        var transform = LinearTransform.Rotation(angle);
        var (w, h) = _unturnedSize;
        var (width, height) = transform.SizeOf(w, h);
        var x = (int)Math.Floor(centreX - width / 2.0 + 0.5);
        var y = (int)Math.Floor(centreY - height / 2.0 + 0.5);
        if (preview)
        {
            _preview = (angle, new IntRect(x, y, width, height));
            return dirty.Union(Show(context));
        }

        _preview = null;
        _lifted = PixelWarp.Warp(_unturned, w, h, transform, width, height);
        _mask = PixelWarp.Warp(_unturnedMask, w, h, transform, width, height);
        (_a, _b, _angle, _turnCentre) = ((x, y), (x + width - 1, y + height - 1), angle, (centreX, centreY));
        _source = Bounds;
        return dirty.Union(Show(context));
    }

    // Replaces a preview of the turn with the exact turn; returns the changed rectangle.
    private IntRect Settle(ToolContext context) =>
        _preview is { } preview ? Turn(context, preview.Angle, _turnCentre.X, _turnCentre.Y) : IntRect.Empty;

    private void SetShape(IntRect bounds, bool[]? mask)
    {
        (_selected, _a, _b) = (true, (bounds.X, bounds.Y), (bounds.Right - 1, bounds.Bottom - 1));
        (_source, _mask, _shownMask, _unturned) = (bounds, mask, mask, null);
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
        Rgba? hidden = _settings.Transparent ? context.SecondaryColor : null;
        if (_preview is { } preview)
        {
            var (w, h) = _unturnedSize;
            return context.Overlay.ShowTurned(preview.Frame, _unturned!, w, h, LinearTransform.Rotation(preview.Angle), hidden);
        }

        var bounds = Bounds;
        var resized = (bounds.Width, bounds.Height) != (_source.Width, _source.Height);
        _shown = resized ? Stretch(_lifted!, _source, bounds) : _lifted!;
        _shownMask = resized && _mask is not null ? Stretch(_mask, _source, bounds) : _mask;
        if (hidden is { } colour && Array.IndexOf(_shown, colour) >= 0)
        {
            _shown = Array.ConvertAll(_shown, pixel => pixel == colour ? Rgba.Transparent : pixel);
        }

        return context.Overlay.Show(bounds, _shown);
    }

    // Nearest neighbour; large bitmaps (a stretch drag) are done in parallel rows.
    private static T[] Stretch<T>(T[] source, IntRect from, IntRect to)
    {
        var result = GC.AllocateUninitializedArray<T>(to.Width * to.Height); // every element is written below
        var columns = new int[to.Width];
        for (var x = 0; x < to.Width; x++)
        {
            columns[x] = (int)((long)x * from.Width / to.Width);
        }

        void Row(int y)
        {
            var row = source.AsSpan((int)((long)y * from.Height / to.Height) * from.Width, from.Width);
            var target = result.AsSpan(y * to.Width, to.Width);
            for (var x = 0; x < target.Length; x++)
            {
                target[x] = row[columns[x]];
            }
        }

        if (result.Length < 1 << 16)
        {
            for (var y = 0; y < to.Height; y++)
            {
                Row(y);
            }
        }
        else
        {
            Parallel.For(0, to.Height, Row);
        }

        return result;
    }

    private static (int X, int Y) Clamp(IPixelAccess target, (int X, int Y) pixel) =>
        (Math.Clamp(pixel.X, 0, target.Width - 1), Math.Clamp(pixel.Y, 0, target.Height - 1));
}
