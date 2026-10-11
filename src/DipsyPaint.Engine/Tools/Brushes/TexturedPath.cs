namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Where a textured tip's texture lies: a point's <c>across</c> coordinate is its signed distance
/// from the line through (<see cref="OriginX"/>, <see cref="OriginY"/>) in the unit
/// <see cref="DirectionX"/>, <see cref="DirectionY"/> (positive to its right on screen, y down), and
/// its <c>along</c> coordinate is <see cref="Along"/> at the origin plus its distance along that line.
/// </summary>
public readonly record struct TextureFrame(double OriginX, double OriginY, double DirectionX, double DirectionY, double Along)
{
    /// <summary>The frame of a straight segment that starts <paramref name="along"/> pixels along its path; a dab's runs left to right.</summary>
    public static TextureFrame ForSegment(double x0, double y0, double x1, double y1, double along)
    {
        double dx = x1 - x0, dy = y1 - y0;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return length > 0 ? new(x0, y0, dx / length, dy / length, along) : new(x0, y0, 1, 0, along);
    }
}

/// <summary>
/// Paints a <see cref="TexturedTip"/> into a <see cref="PassStroke"/>: swept along a segment or a
/// polyline, or spread over an area. This is where a textured tip's coverage is computed, for the
/// oil and watercolour brushes and for shapes drawn with their texture.
/// </summary>
public static class TexturedPath
{
    /// <summary>
    /// Sweeps <paramref name="tip"/> in a straight line from (x0, y0) to (x1, y1), in document
    /// pixels, round at both ends, with its texture laid in <paramref name="frame"/>. The tip's size
    /// is scaled by <paramref name="startScale"/> at the start, changing evenly to
    /// <paramref name="endScale"/> at the end (pen pressure), so the sweep tapers between them.
    /// </summary>
    public static void Segment(
        PassStroke stroke, TexturedTip tip, double x0, double y0, double x1, double y1, TextureFrame frame,
        double startScale = 1, double endScale = 1)
    {
        var target = stroke.Target;
        var reach = tip.Radius * Math.Max(startScale, endScale) + 0.5;
        var left = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach));
        var top = Math.Max(stroke.Top, (int)Math.Floor(Math.Min(y0, y1) - reach));
        var right = Math.Min(target.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
        var bottom = Math.Min(target.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));

        double dx = x1 - x0, dy = y1 - y0;
        var length = Math.Sqrt(dx * dx + dy * dy);
        double ux = length > 0 ? dx / length : 1, uy = length > 0 ? dy / length : 0;
        var scaleChange = endScale - startScale;
        for (var py = top; py <= bottom; py++)
        {
            for (var px = left; px <= right; px++)
            {
                double cx = px + 0.5 - x0, cy = py + 0.5 - y0;
                var projection = cx * ux + cy * uy;
                var t = Math.Clamp(projection, 0, length);
                var scale = startScale;
                var nearest = t;
                if (scaleChange != 0)
                {
                    // Measure from the point of the tapering sweep that reaches furthest towards the pixel.
                    var fraction = PenPressure.Nearest(cx, cy, dx, dy, tip.Radius * scaleChange);
                    (nearest, scale) = (fraction * length, startScale + fraction * scaleChange);
                }

                double ex = cx - nearest * ux, ey = cy - nearest * uy;
                var distance = Math.Sqrt(ex * ex + ey * ey);
                if (distance >= tip.Radius * scale + 0.5)
                {
                    continue;
                }

                double fx = px + 0.5 - frame.OriginX, fy = py + 0.5 - frame.OriginY;
                var across = fy * frame.DirectionX - fx * frame.DirectionY;
                var along = frame.Along + fx * frame.DirectionX + fy * frame.DirectionY;
                var tipReach = tip.Reach(along, across < 0 ? -1 : 1) * scale;
                var rim = (tipReach - distance + 0.5) / tip.Softness;
                if (rim <= 0)
                {
                    continue;
                }

                var texture = tip.Texture(across, along, Math.Max(tipReach - Math.Abs(across), 0));
                stroke.Cover(px, py, (float)Math.Min(rim, 1), (float)texture, along, (float)Math.Abs(projection - t));
            }
        }
    }

    /// <summary>Sweeps <paramref name="tip"/> through <paramref name="points"/> in order; one point is a dab.</summary>
    public static void Polyline(PassStroke stroke, TexturedTip tip, IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count == 1)
        {
            var (x, y) = points[0];
            Segment(stroke, tip, x, y, x, y, TextureFrame.ForSegment(x, y, x, y, 0));
        }

        var along = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var (x0, y0) = points[i - 1];
            var (x1, y1) = points[i];
            Segment(stroke, tip, x0, y0, x1, y1, TextureFrame.ForSegment(x0, y0, x1, y1, along));
            along += Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        }
    }

    /// <summary>
    /// Spreads <paramref name="tip"/>'s texture over the pixels of <paramref name="bounds"/>, each
    /// weighted by <paramref name="coverage"/>(x, y) (0–1, e.g. a shape's antialiased interior), as
    /// if the tip had been dragged across the area from left to right in rows.
    /// </summary>
    public static void Area(PassStroke stroke, TexturedTip tip, IntRect bounds, Func<int, int, float> coverage)
    {
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                var weight = coverage(x, y);
                if (weight > 0)
                {
                    stroke.Cover(x, y, weight, (float)tip.Texture(y + 0.5 - bounds.Y, x + 0.5 - bounds.X, double.PositiveInfinity), 0);
                }
            }
        }
    }
}
