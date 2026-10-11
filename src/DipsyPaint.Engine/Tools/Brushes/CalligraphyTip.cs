namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// A flat calligraphy nib held at a fixed 45°: a line <c>size</c> pixels long and about a sixth of
/// that thick. Moving it sweeps a parallelogram, so strokes across the nib are broad and strokes
/// along it are thin. Solid ink with a one-pixel antialiased edge.
/// </summary>
internal sealed class CalligraphyTip(bool rising) : BrushTip
{
    private const double HalfSqrt2 = 0.70710678118654752;

    public override void Segment(Stroke stroke, double x0, double y0, double x1, double y1, double size)
    {
        // The nib's half-length vector; y grows downwards, so rising (/) means up and to the right.
        var ux = size / 2 * HalfSqrt2;
        var uy = rising ? -ux : ux;
        var radius = Math.Max(size / 6, 1) / 2;
        var reach = Math.Abs(ux) + radius + 0.5;
        var left = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach));
        var top = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - reach));
        var right = Math.Min(stroke.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
        var bottom = Math.Min(stroke.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));

        // The swept parallelogram's corners, in order: both ends of the nib at the start, then at the end.
        Span<double> cornerX = [x0 - ux, x0 + ux, x1 + ux, x1 - ux];
        Span<double> cornerY = [y0 - uy, y0 + uy, y1 + uy, y1 - uy];
        double dx = x1 - x0, dy = y1 - y0;
        var hasArea = Math.Abs(dx * uy - dy * ux) > 1e-9;
        for (var py = top; py <= bottom; py++)
        {
            for (var px = left; px <= right; px++)
            {
                var distance = Distance(px + 0.5, py + 0.5, cornerX, cornerY, hasArea);
                var coverage = radius + 0.5 - distance;
                if (coverage > 0)
                {
                    stroke.Cover(px, py, (float)Math.Min(coverage, 1));
                }
            }
        }
    }

    /// <summary>Solid ink.</summary>
    public override void Fill(Stroke stroke, int x, int y, float coverage) => stroke.Cover(x, y, coverage);

    /// <summary>Distance from (px, py) to the parallelogram; 0 inside it.</summary>
    private static double Distance(double px, double py, ReadOnlySpan<double> cornerX, ReadOnlySpan<double> cornerY, bool hasArea)
    {
        var nearest = double.MaxValue;
        bool anyLeft = false, anyRight = false;
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) & 3;
            double ax = cornerX[i], ay = cornerY[i];
            double ex = cornerX[j] - ax, ey = cornerY[j] - ay;
            double qx = px - ax, qy = py - ay;
            var side = ex * qy - ey * qx;
            anyLeft |= side < 0;
            anyRight |= side > 0;

            var lengthSquared = ex * ex + ey * ey;
            var t = lengthSquared == 0 ? 0 : Math.Clamp((qx * ex + qy * ey) / lengthSquared, 0, 1);
            double rx = qx - t * ex, ry = qy - t * ey;
            nearest = Math.Min(nearest, rx * rx + ry * ry);
        }

        // Inside a convex polygon the point lies on the same side of every edge. A degenerate
        // parallelogram (no movement, or movement along the nib) is just its edges.
        return hasArea && !(anyLeft && anyRight) ? 0 : Math.Sqrt(nearest);
    }
}
