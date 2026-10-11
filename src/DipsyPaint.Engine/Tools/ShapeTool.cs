namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Draws the shapes of reference Paint's gallery. Dragging draws the shape (Shift keeps it square,
/// circular or at a multiple of 45°); it then stays pending in the overlay, where dragging a handle
/// resizes it and dragging inside moves it, until it is committed (a press outside it, which also
/// starts the next shape; see <see cref="IPendingTool"/>). The left button outlines in the primary
/// colour and fills in the secondary; the right button swaps them.
/// <para>
/// A curve and a polygon take several gestures, as in reference Paint. A curve starts as a dragged
/// line; the next two presses bend it (drag to adjust each bend). A polygon starts with a dragged
/// side; each further press adds a corner (drag to place it, Shift for 45° sides), and a click on
/// the last corner (a double-click) or near the first one closes it. While they are being built
/// <see cref="Adornment"/> is null, so presses reach the tool, and <see cref="HasPending"/> is true.
/// Once finished they get a frame like any other shape; resizing it scales their points.
/// </para>
/// <para>
/// Dragging the rotate handle, or <see cref="Rotate"/>, turns a pending shape to any angle about its
/// frame's centre. Its outline is turned before it is rasterised, so edges stay hard, and the frame
/// becomes the upright rectangle around the result; resizing then stretches the turned shape. A
/// turned box shape keeps its outline as contours, scaled to the frame as a polygon's points are.
/// </para>
/// </summary>
public sealed class ShapeTool(ShapeSettings settings) : IRotatableTool
{
    private enum Gesture
    {
        None,
        Draw,
        Adjust,
        Build,
        Rotate,
    }

    private bool _pending;
    private ShapeKind _kind;
    private bool _swapColors;

    // The frame corners (box shapes, finished curves and polygons) or end points (line, and the
    // first drag of a curve or polygon), in pixels, inclusive.
    private (int X, int Y) _a;
    private (int X, int Y) _b;

    // A curve's start, two control points and end (cubic Bézier), or a polygon's corners, in pixels.
    // Once finished they are drawn scaled from _pointsFrame to the current frame.
    private readonly List<(double X, double Y)> _points = [];
    private IntRect _pointsFrame;
    private bool _building;
    private int _bends;

    // A turned box shape's outlines (pixel edges at whole numbers) in _pointsFrame; null while upright.
    private List<(double X, double Y)[]>? _contours;

    // The angle turned so far, and while the rotate handle is dragged, the shape as it was at the press.
    private double _angle;
    private RotateDrag? _rotateDrag;
    private List<(double X, double Y)[]> _dragGeometry = [];
    private (double X, double Y) _dragCentre;

    private Gesture _gesture;
    private FrameHandle _handle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _dragA;
    private (int X, int Y) _dragB;

    public Adornment? Adornment =>
        _pending && !_building && _gesture is not (Gesture.Draw or Gesture.Build) ? new Adornment(Frame) : null;

    /// <summary>True while a shape is pending, including a curve or polygon that is still being built.</summary>
    public bool HasPending => _pending;

    private IntRect Frame => Engine.Adornment.FrameOf(_a, _b);

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var pixel = (X: (int)Math.Floor(input.X), Y: (int)Math.Floor(input.Y));
        if (input.Action == PointerAction.Down)
        {
            if (_building)
            {
                _gesture = Gesture.Build;
                if (_kind == ShapeKind.Polygon)
                {
                    _points.Add(_points[^1]);
                }

                Build(pixel, input.Shift);
                return Render(context);
            }

            var hit = Adornment?.HitTest(input.X, input.Y, context.HandleTolerance) ?? FrameHandle.None;
            if (hit == FrameHandle.Rotate)
            {
                var frame = Frame;
                var drag = new RotateDrag(frame.X + frame.Width / 2.0, frame.Y + frame.Height / 2.0, input, _angle);
                (_gesture, _rotateDrag, _dragGeometry, _dragCentre) = (Gesture.Rotate, drag, Geometry(), Centre);
                return IntRect.Empty;
            }

            if (hit != FrameHandle.None)
            {
                (_gesture, _handle, _dragStart, _dragA, _dragB) = (Gesture.Adjust, hit, pixel, _a, _b);
                return IntRect.Empty;
            }

            // A press outside: the editor has committed the previous shape; draw a new one.
            (_gesture, _pending, _kind, _swapColors, _a, _b) =
                (Gesture.Draw, true, settings.Kind, input.Button == PointerButton.Right, pixel, pixel);
            (_contours, _angle) = (null, 0);
            _points.Clear();
            return context.Overlay.Clear();
        }

        switch (_gesture)
        {
            case Gesture.Draw:
                _b = input.Shift ? Constrain(_a, pixel) : pixel;
                break;
            case Gesture.Adjust:
                (_a, _b) = Engine.Adornment.Drag(_dragA, _dragB, _handle, pixel.X - _dragStart.X, pixel.Y - _dragStart.Y);
                break;
            case Gesture.Build:
                Build(pixel, input.Shift);
                break;
            case Gesture.Rotate:
                var angle = _rotateDrag!.AngleAt(input);
                if (angle == _angle)
                {
                    return IntRect.Empty;
                }

                SetGeometry(Turned(_dragGeometry, angle - _rotateDrag.StartAngle, _dragCentre.X, _dragCentre.Y));
                _angle = angle;
                break;
            default:
                return IntRect.Empty;
        }

        if (input.Action == PointerAction.Up)
        {
            var gesture = _gesture;
            _gesture = Gesture.None;
            if (gesture == Gesture.Draw && _a == _b)
            {
                return Cancel(context); // a click draws no shape
            }

            if (gesture == Gesture.Draw && _kind is ShapeKind.Curve or ShapeKind.Polygon)
            {
                StartBuilding();
            }
            else if (gesture == Gesture.Build)
            {
                EndBuildStep(context.HandleTolerance);
            }
        }

        return Render(context);
    }

    public IntRect Commit(ToolContext context)
    {
        if (!_pending)
        {
            return IntRect.Empty;
        }

        var dirty = IntRect.Empty;
        if (_building)
        {
            Finish(); // a polygon closes
            dirty = Render(context);
        }

        dirty = dirty.Union(context.Overlay.DrawOnto(context.Target));
        return dirty.Union(Cancel(context));
    }

    public IntRect Cancel(ToolContext context)
    {
        if (!_pending)
        {
            return IntRect.Empty;
        }

        _pending = false;
        _building = false;
        _gesture = Gesture.None;
        return context.Overlay.Clear();
    }

    public IntRect Refresh(ToolContext context) => _pending && (_gesture != Gesture.Draw || _a != _b) ? Render(context) : IntRect.Empty;

    /// <summary>
    /// Turns the pending shape <paramref name="degrees"/> clockwise about its frame's centre (see the
    /// class remarks). Returns the changed rectangle; empty unless a finished shape is pending.
    /// </summary>
    public IntRect Rotate(ToolContext context, double degrees)
    {
        if (Adornment is null)
        {
            return IntRect.Empty;
        }

        var (centreX, centreY) = Centre;
        SetGeometry(Turned(Geometry(), degrees, centreX, centreY));
        _angle = Math.IEEERemainder(_angle + degrees, 360);
        return Render(context);
    }

    // Lines, curves and polygons are points through pixel centres; box shapes are outlines along pixel edges.
    private bool IsPath => _kind is ShapeKind.Line or ShapeKind.Curve or ShapeKind.Polygon;

    // The frame's centre in the coordinates of Geometry().
    private (double X, double Y) Centre
    {
        get
        {
            var frame = Frame;
            var offset = IsPath ? 0.5 : 0;
            return (frame.X + frame.Width / 2.0 - offset, frame.Y + frame.Height / 2.0 - offset);
        }
    }

    // The pending shape as placed: a line's end points, a curve's or polygon's points, or a box shape's outlines.
    private List<(double X, double Y)[]> Geometry()
    {
        var frame = Frame;
        return _kind switch
        {
            ShapeKind.Line => [[_a, _b]],
            ShapeKind.Curve or ShapeKind.Polygon => [[.. ScaledPoints()]],
            _ when _contours is not null => ScaledContours(),
            _ => [.. ShapeOutlines.Of(_kind, frame.Width, frame.Height).Select(c => c.Select(p => (frame.X + p.X, frame.Y + p.Y)).ToArray())],
        };
    }

    // Makes the geometry the pending shape, with the frame around it.
    private void SetGeometry(List<(double X, double Y)[]> geometry)
    {
        switch (_kind)
        {
            case ShapeKind.Line:
                (_a, _b) = (ShapeRasterizer.Round(geometry[0][0]), ShapeRasterizer.Round(geometry[0][1]));
                break;
            case ShapeKind.Curve or ShapeKind.Polygon:
                _points.Clear();
                _points.AddRange(geometry[0]);
                FitFrame();
                break;
            default:
                // The pixels whose centres lie within the outlines' extent.
                var all = geometry.SelectMany(c => c).ToList();
                _a = ((int)Math.Ceiling(all.Min(p => p.X) - 0.5), (int)Math.Ceiling(all.Min(p => p.Y) - 0.5));
                _b = (Math.Max(_a.X, (int)Math.Floor(all.Max(p => p.X) - 0.5)), Math.Max(_a.Y, (int)Math.Floor(all.Max(p => p.Y) - 0.5)));
                (_contours, _pointsFrame) = (geometry, Frame);
                break;
        }
    }

    private static List<(double X, double Y)[]> Turned(List<(double X, double Y)[]> geometry, double degrees, double centreX, double centreY)
    {
        var transform = LinearTransform.Rotation(degrees);
        return [.. geometry.Select(c => c.Select(p => transform.Apply(p, centreX, centreY)).ToArray())];
    }

    // A turned box shape's outlines mapped from the frame they were turned in to the current one.
    private List<(double X, double Y)[]> ScaledContours()
    {
        var (from, to) = (_pointsFrame, Frame);
        double sx = (double)to.Width / from.Width, sy = (double)to.Height / from.Height;
        return [.. _contours!.Select(c => c.Select(p => (to.X + (p.X - from.X) * sx, to.Y + (p.Y - from.Y) * sy)).ToArray())];
    }

    private void StartBuilding()
    {
        _building = true;
        _bends = 0;
        _points.Clear();
        _points.AddRange(_kind == ShapeKind.Curve ? [_a, _a, _b, _b] : [_a, _b]); // a straight curve has its controls at its ends
    }

    // Moves the bend or corner being placed to the pointer.
    private void Build((int X, int Y) pixel, bool shift)
    {
        if (_kind == ShapeKind.Curve)
        {
            // The first bend pulls both control points; the second only the one nearer the end.
            if (_bends == 0)
            {
                _points[1] = pixel;
            }

            _points[2] = pixel;
            return;
        }

        var previous = ShapeRasterizer.Round(_points[^2]);
        _points[^1] = shift ? ConstrainLine(previous, pixel) : pixel;
    }

    private void EndBuildStep(double closeDistance)
    {
        if (_kind == ShapeKind.Curve)
        {
            if (++_bends == 2)
            {
                Finish();
            }

            return;
        }

        // A click on the last corner (the second click of a double-click), or near the first one, closes the polygon.
        var (corner, previous, first) = (_points[^1], _points[^2], _points[0]);
        if (Math.Max(Math.Abs(corner.X - previous.X), Math.Abs(corner.Y - previous.Y)) <= 1
            || (_points.Count > 3 && Math.Max(Math.Abs(corner.X - first.X), Math.Abs(corner.Y - first.Y)) <= Math.Max(1, closeDistance)))
        {
            _points.RemoveAt(_points.Count - 1);
            Finish();
        }
    }

    // A curve or polygon is complete: it gets a frame, which then scales its points.
    private void Finish()
    {
        _building = false;
        FitFrame();
    }

    // Frames a curve's or polygon's points.
    private void FitFrame()
    {
        IEnumerable<(double X, double Y)> placed = _kind == ShapeKind.Curve ? CurvePoints(_points) : _points;
        var pixels = placed.Select(ShapeRasterizer.Round).ToList();
        _a = (pixels.Min(p => p.X), pixels.Min(p => p.Y));
        _b = (pixels.Max(p => p.X), pixels.Max(p => p.Y));
        _pointsFrame = Frame;
    }

    // The points mapped from the frame they were placed in to the current one.
    private List<(double X, double Y)> ScaledPoints()
    {
        if (_building)
        {
            return _points;
        }

        var (from, to) = (_pointsFrame, Frame);
        double Scale(int fromExtent, int toExtent) => fromExtent <= 1 ? 1 : (toExtent - 1.0) / (fromExtent - 1);
        double sx = Scale(from.Width, to.Width), sy = Scale(from.Height, to.Height);
        return _points.Select(p => (to.X + (p.X - from.X) * sx, to.Y + (p.Y - from.Y) * sy)).ToList();
    }

    private static (double X, double Y)[] CurvePoints(IReadOnlyList<(double X, double Y)> controls)
    {
        double length = 0;
        for (var i = 1; i < 4; i++)
        {
            length += Math.Abs(controls[i].X - controls[i - 1].X) + Math.Abs(controls[i].Y - controls[i - 1].Y);
        }

        var segments = Math.Clamp((int)Math.Ceiling(length / 6), 2, 256);
        return ShapeOutlines.FlattenCubic(controls[0], controls[1], controls[2], controls[3], segments);
    }

    private IntRect Render(ToolContext context)
    {
        var (outline, fill) = _swapColors
            ? (context.SecondaryColor, context.PrimaryColor)
            : (context.PrimaryColor, context.SecondaryColor);
        var (bounds, pixels) = RenderShape(outline, fill);
        return context.Overlay.Show(bounds, pixels);
    }

    private (IntRect Bounds, Rgba[] Pixels) RenderShape(Rgba outline, Rgba fill)
    {
        var size = Math.Max(1, settings.Size);
        if (_contours is not null)
        {
            var bounds = Frame;
            var outlineSize = settings.Outline == ShapeStyle.None ? 0 : size;
            var paint = settings.Fill == ShapeStyle.None ? Rgba.Transparent : fill;
            return (bounds, ShapeRasterizer.Closed(bounds, ScaledContours(), outlineSize, outline, paint));
        }

        if (_kind is not (ShapeKind.Curve or ShapeKind.Polygon) || _gesture == Gesture.Draw)
        {
            var kind = _kind is ShapeKind.Curve or ShapeKind.Polygon ? ShapeKind.Line : _kind;
            return ShapeRasterizer.Render(kind, _a, _b, settings.Outline, settings.Fill, size, outline, fill);
        }

        var points = ScaledPoints();
        if (_kind == ShapeKind.Curve)
        {
            return settings.Outline == ShapeStyle.None ? (IntRect.Empty, []) : ShapeRasterizer.Polyline(CurvePoints(points), size, outline);
        }

        // A polygon being built shows its sides; with no outline, as thin lines in the fill colour.
        // So does one with no inside (fewer than three corners, or all in one row or column).
        var frame = Frame;
        if (_building || points.Count < 3 || frame.Width < 2 || frame.Height < 2)
        {
            return settings.Outline != ShapeStyle.None ? ShapeRasterizer.Polyline(points, size, outline)
                : settings.Fill != ShapeStyle.None ? ShapeRasterizer.Polyline(points, 1, fill)
                : (IntRect.Empty, []);
        }

        // Corner pixels map onto the frame's outer edges, so the outline covers them, as a rectangle's does.
        double sx = frame.Width / (frame.Width - 1.0), sy = frame.Height / (frame.Height - 1.0);
        (double X, double Y)[] polygon = [.. points.Select(p => (frame.X + (p.X - frame.X) * sx, frame.Y + (p.Y - frame.Y) * sy))];
        var thickness = settings.Outline == ShapeStyle.None ? 0 : size;
        var fillPaint = settings.Fill == ShapeStyle.None ? Rgba.Transparent : fill;
        return (frame, ShapeRasterizer.Closed(frame, [polygon], thickness, outline, fillPaint));
    }

    /// <summary>Square or circle for a box; horizontal, vertical or 45° for a line.</summary>
    private (int X, int Y) Constrain((int X, int Y) from, (int X, int Y) to)
    {
        if (_kind is ShapeKind.Line or ShapeKind.Curve or ShapeKind.Polygon)
        {
            return ConstrainLine(from, to);
        }

        int dx = to.X - from.X, dy = to.Y - from.Y;
        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return (from.X + (dx < 0 ? -side : side), from.Y + (dy < 0 ? -side : side));
    }

    private static (int X, int Y) ConstrainLine((int X, int Y) from, (int X, int Y) to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        const double tan22_5 = 0.41421356;
        if (Math.Abs(dy) <= Math.Abs(dx) * tan22_5)
        {
            return (to.X, from.Y);
        }

        if (Math.Abs(dx) <= Math.Abs(dy) * tan22_5)
        {
            return (from.X, to.Y);
        }

        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return (from.X + (dx < 0 ? -side : side), from.Y + (dy < 0 ? -side : side));
    }
}
