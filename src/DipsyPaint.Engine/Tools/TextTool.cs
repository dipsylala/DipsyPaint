namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Adds text as in reference Paint. A click opens a text box <see cref="DefaultWidth"/> pixels wide
/// at that point; a drag opens one the size of the dragged rectangle. The box stays pending, shown
/// in the overlay, while the shell edits <see cref="Text"/> in place: lines wrap at the box's width
/// and the box grows downwards to fit them. Dragging the frame around the box moves it and dragging
/// a handle resizes it. The text is set in the primary colour, on the secondary colour when
/// <see cref="TextSettings.FillBackground"/> is on, and every option applies to all of it. Committing
/// writes the rendered pixels to the layer as one history step; empty text writes nothing. A press
/// outside the box commits it without opening a new one. The right button does nothing.
/// <para>
/// Dragging the rotate handle, or <see cref="Rotate"/>, turns the text box to any angle about its
/// centre: the text is rendered upright and then turned (bilinear; while the handle is dragged, a quick
/// nearest-neighbour preview), and the frame becomes the upright rectangle around it. A turned box can be moved, stretched (which stretches the turned text) and
/// restyled, but not edited: <see cref="TextBounds"/> is null, so the shell hides its editor.
/// </para>
/// </summary>
public sealed class TextTool(TextSettings settings, ITextRenderer renderer) : IRotatableTool
{
    /// <summary>Width, in pixels, of a text box opened with a click.</summary>
    public const int DefaultWidth = 200;

    private enum Gesture
    {
        None,
        Draw,
        Adjust,
        Rotate,

        // A press that only committed the previous box, or a right-button press.
        Ignore,
    }

    private bool _pending;
    private Gesture _gesture;

    // The box as placed by the user (its height is a minimum) and as rendered (grown to fit the text).
    private IntRect _area;
    private IntRect _bounds;

    // Band between the box and its frame, in document pixels: the part of the frame the pointer can
    // grab to move the box, since the shell's text editor covers the box itself. Follows the zoom.
    private int _margin = MarginFor(DocumentEditor.DefaultHandleTolerance);

    private (int X, int Y) _dragStart;
    private FrameHandle _handle;
    private IntRect _dragFrame;

    // A turned box: how the upright rendering maps onto the shown one, the centre it is shown about and
    // the angle turned. Identity while upright. At a press, what they were (for the drag to build on).
    private LinearTransform _transform = LinearTransform.Identity;
    private (double X, double Y) _centre;
    private double _angle;
    private LinearTransform _dragTransform;
    private (double X, double Y) _dragCentre;
    private RotateDrag? _rotateDrag;

    // While the rotate handle is dragged: the upright rendering, kept because only the turn changes, and
    // shown turned as a quick preview (Overlay.ShowTurned). Releasing the handle renders exactly. Null otherwise.
    private PixelImage? _upright;

    /// <summary>Raised when the pending text box appears, moves, resizes, is re-rendered or goes away.</summary>
    public event EventHandler? Changed;

    public TextSettings Settings => settings;

    /// <summary>
    /// The pending text. The shell sets it as the user types, then calls
    /// <see cref="DocumentEditor.RefreshPending"/> to re-render it. Cleared when a new box opens.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Where the pending text box is, in document pixels, while it can be edited (between gestures);
    /// null otherwise. Its height includes the growth needed by the text.
    /// </summary>
    public IntRect? TextBounds => _pending && _gesture != Gesture.Draw && _transform.IsIdentity ? _bounds : null;

    public Adornment? Adornment => _pending ? new Adornment(Inflate(_bounds, _margin)) : null;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        _margin = MarginFor(context.HandleTolerance);
        var pixel = (X: (int)Math.Floor(input.X), Y: (int)Math.Floor(input.Y));
        if (input.Action == PointerAction.Down)
        {
            var hit = Adornment?.HitTest(input.X, input.Y, context.HandleTolerance) ?? FrameHandle.None;
            if (hit != FrameHandle.None)
            {
                (_dragTransform, _dragCentre) = (_transform, Centre);
                if (hit == FrameHandle.Rotate)
                {
                    (_gesture, _rotateDrag) = (Gesture.Rotate, new RotateDrag(_dragCentre.X, _dragCentre.Y, input, _angle));
                    return IntRect.Empty;
                }

                (_gesture, _handle, _dragStart, _dragFrame) = (Gesture.Adjust, hit, pixel, Inflate(_bounds, _margin));
                return IntRect.Empty;
            }

            if (context.CommittedByPress || input.Button != PointerButton.Left)
            {
                _gesture = Gesture.Ignore;
                return IntRect.Empty;
            }

            (_gesture, _pending, _dragStart, Text) = (Gesture.Draw, true, pixel, "");
            (_transform, _angle) = (LinearTransform.Identity, 0);
            _area = new IntRect(pixel.X, pixel.Y, 1, 1);
            return Render(context);
        }

        switch (_gesture)
        {
            case Gesture.Draw when input.Action == PointerAction.Up && pixel == _dragStart:
                _area = new IntRect(pixel.X, pixel.Y, DefaultWidth, 1); // a click
                break;
            case Gesture.Draw:
                _area = Engine.Adornment.FrameOf(_dragStart, pixel);
                break;
            case Gesture.Rotate:
                var angle = _rotateDrag!.AngleAt(input);
                if (angle == _angle && input.Action != PointerAction.Up)
                {
                    return IntRect.Empty;
                }

                _transform = _dragTransform.Then(LinearTransform.Rotation(angle - _rotateDrag.StartAngle));
                (_centre, _angle) = (_dragCentre, angle);
                break;
            case Gesture.Adjust when !_transform.IsIdentity:
                AdjustTurned(pixel);
                break;
            case Gesture.Adjust:
                var frame = _dragFrame;
                var (a, b) = Engine.Adornment.Drag(
                    (frame.X, frame.Y), (frame.Right - 1, frame.Bottom - 1), _handle, pixel.X - _dragStart.X, pixel.Y - _dragStart.Y);
                var box = Deflate(Engine.Adornment.FrameOf(a, b), _margin);
                // The height a resize leaves is a minimum; moving keeps the box's own minimum.
                _area = _handle == FrameHandle.Move ? _area with { X = box.X, Y = box.Y } : box;
                break;
            default:
                if (input.Action == PointerAction.Up)
                {
                    _gesture = Gesture.None;
                }

                return IntRect.Empty;
        }

        if (input.Action == PointerAction.Up)
        {
            _gesture = Gesture.None;
        }

        return Render(context, preview: _gesture == Gesture.Rotate);
    }

    public IntRect Commit(ToolContext context)
    {
        if (!_pending)
        {
            return IntRect.Empty;
        }

        var dirty = _upright is null ? IntRect.Empty : Render(context); // a preview: render exactly first
        dirty = dirty.Union(Text.Length > 0 ? context.Overlay.DrawOnto(context.Target) : IntRect.Empty);
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
        _upright = null;
        var dirty = context.Overlay.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        return dirty;
    }

    public IntRect Refresh(ToolContext context)
    {
        _margin = MarginFor(context.HandleTolerance);
        return _pending ? Render(context) : IntRect.Empty;
    }

    /// <summary>
    /// Turns the pending text box <paramref name="degrees"/> clockwise about its centre (see the class
    /// remarks). Returns the changed rectangle; empty when no box is pending.
    /// </summary>
    public IntRect Rotate(ToolContext context, double degrees)
    {
        if (!_pending || _gesture != Gesture.None)
        {
            return IntRect.Empty;
        }

        _centre = Centre;
        _transform = _transform.Then(LinearTransform.Rotation(degrees));
        _angle = Math.IEEERemainder(_angle + degrees, 360);
        return Render(context);
    }

    private (double X, double Y) Centre => (_bounds.X + _bounds.Width / 2.0, _bounds.Y + _bounds.Height / 2.0);

    // A drag of a turned box's frame: moving moves its centre; a handle stretches the turned text to the new frame.
    private void AdjustTurned((int X, int Y) pixel)
    {
        var start = _dragFrame;
        var (a, b) = Engine.Adornment.Drag(
            (start.X, start.Y), (start.Right - 1, start.Bottom - 1), _handle, pixel.X - _dragStart.X, pixel.Y - _dragStart.Y);
        var (from, to) = (Deflate(start, _margin), Deflate(Engine.Adornment.FrameOf(a, b), _margin));
        _transform = _handle == FrameHandle.Move
            ? _dragTransform
            : _dragTransform.Then(LinearTransform.Scale((double)to.Width / from.Width, (double)to.Height / from.Height));
        _centre = (_dragCentre.X + to.X + to.Width / 2.0 - (from.X + from.Width / 2.0), _dragCentre.Y + to.Y + to.Height / 2.0 - (from.Y + from.Height / 2.0));
    }

    // Renders the text, turned exactly (bilinear) or, with `preview`, as a quick preview (see _upright).
    private IntRect Render(ToolContext context, bool preview = false)
    {
        var request = new TextRequest(
            Text, settings.ToStyle(), _area.Width, _area.Height, context.PrimaryColor,
            settings.FillBackground ? context.SecondaryColor : null);
        var image = preview && _upright is { } kept ? kept : renderer.Render(request);
        _upright = preview ? image : null;
        var pixels = image.Pixels;
        _bounds = new IntRect(_area.X, _area.Y, image.Width, image.Height);
        IntRect dirty;
        if (_transform.IsIdentity)
        {
            dirty = context.Overlay.Show(_bounds, pixels);
        }
        else
        {
            var (width, height) = _transform.SizeOf(image.Width, image.Height);
            _bounds = new IntRect(
                (int)Math.Floor(_centre.X - width / 2.0 + 0.5), (int)Math.Floor(_centre.Y - height / 2.0 + 0.5), width, height);
            dirty = preview
                ? context.Overlay.ShowTurned(_bounds, pixels, image.Width, image.Height, _transform, hidden: null)
                : context.Overlay.Show(_bounds, PixelWarp.Warp(pixels, image.Width, image.Height, _transform, width, height));
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return dirty;
    }

    private static int MarginFor(double handleTolerance) => Math.Max(1, (int)Math.Ceiling(handleTolerance));

    private static IntRect Inflate(IntRect rect, int by) => new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);

    // Never smaller than one pixel, so a box dragged smaller than its frame stays usable.
    private static IntRect Deflate(IntRect rect, int by) =>
        new(rect.X + by, rect.Y + by, Math.Max(1, rect.Width - 2 * by), Math.Max(1, rect.Height - 2 * by));
}
