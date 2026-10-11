namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's basic brush: a round, antialiased tip of <see cref="StrokeSettings.Size"/>
/// pixels diameter swept along the pointer path, at <see cref="StrokeSettings.Opacity"/>. The left
/// button paints in the primary colour, the right button in the secondary colour. The tip is centred on the pixel under the pointer.
/// </summary>
public sealed class BrushTool(StrokeSettings settings) : ITool
{
    private Stroke? _stroke;
    private double _radius;
    private double _lastX;
    private double _lastY;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = Math.Floor(input.X) + 0.5;
        var y = Math.Floor(input.Y) + 0.5;

        if (input.Action == PointerAction.Down)
        {
            var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
            _stroke = new Stroke(context.Target, color, settings.OpacityFraction);
            _radius = settings.Size / 2.0;
            _lastX = x;
            _lastY = y;
        }
        else if (_stroke is null)
        {
            return IntRect.Empty;
        }

        Sweep(_stroke, context.Target, _lastX, _lastY, x, y, _radius);
        _lastX = x;
        _lastY = y;
        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            _stroke = null;
        }

        return dirty;
    }

    /// <summary>
    /// Covers the capsule swept by a disc of <paramref name="radius"/> from (x0, y0) to (x1, y1).
    /// Each pixel's coverage is how far its centre lies inside the edge, clamped to 0–1, which
    /// gives a one-pixel antialiased rim. Sweeping a whole segment leaves no gaps at fast moves.
    /// </summary>
    private static void Sweep(Stroke stroke, IPixelAccess target, double x0, double y0, double x1, double y1, double radius)
    {
        var reach = radius + 0.5;
        var left = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach));
        var top = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - reach));
        var right = Math.Min(target.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
        var bottom = Math.Min(target.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));

        double dx = x1 - x0, dy = y1 - y0;
        var lengthSquared = dx * dx + dy * dy;
        for (var py = top; py <= bottom; py++)
        {
            for (var px = left; px <= right; px++)
            {
                double cx = px + 0.5 - x0, cy = py + 0.5 - y0;
                var t = lengthSquared == 0 ? 0 : Math.Clamp((cx * dx + cy * dy) / lengthSquared, 0, 1);
                double ex = cx - t * dx, ey = cy - t * dy;
                var coverage = reach - Math.Sqrt(ex * ex + ey * ey);
                if (coverage > 0)
                {
                    stroke.Cover(px, py, (float)Math.Min(coverage, 1));
                }
            }
        }
    }
}
