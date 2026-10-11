namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// A round tip of the given size (diameter) swept along a segment, like the basic brush, whose
/// coverage each subclass shapes from the pixel's distance to the path and the paper grain.
/// </summary>
internal abstract class RoundTip : BrushTip
{
    public override void Segment(Stroke stroke, double x0, double y0, double x1, double y1, double size)
    {
        var radius = size / 2.0;
        var reach = radius + 0.5;
        var left = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach));
        var top = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - reach));
        var right = Math.Min(stroke.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
        var bottom = Math.Min(stroke.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));

        double dx = x1 - x0, dy = y1 - y0;
        var lengthSquared = dx * dx + dy * dy;
        for (var py = top; py <= bottom; py++)
        {
            for (var px = left; px <= right; px++)
            {
                double cx = px + 0.5 - x0, cy = py + 0.5 - y0;
                var t = lengthSquared == 0 ? 0 : Math.Clamp((cx * dx + cy * dy) / lengthSquared, 0, 1);
                double ex = cx - t * dx, ey = cy - t * dy;
                var distance = Math.Sqrt(ex * ex + ey * ey);
                if (distance < reach)
                {
                    stroke.Cover(px, py, Coverage(px, py, distance, radius));
                }
            }
        }
    }

    /// <summary>The texture deep inside the tip, scaled by the area's coverage.</summary>
    public override void Fill(Stroke stroke, int x, int y, float coverage) =>
        stroke.Cover(x, y, coverage * Coverage(x, y, 0, double.PositiveInfinity));

    /// <summary>
    /// Coverage (0–1) of pixel (x, y), whose centre lies <paramref name="distance"/> from the path
    /// of a tip of <paramref name="radius"/>; within half a pixel of the rim it fades out.
    /// </summary>
    protected abstract float Coverage(int x, int y, double distance, double radius);

    /// <summary>The one-pixel antialiased rim of the basic brush: 1 inside, falling to 0 half a pixel outside the tip.</summary>
    protected static float Rim(double distance, double radius) => (float)Math.Clamp(radius + 0.5 - distance, 0, 1);

    /// <summary>How far out from the path the pixel lies, from 0 (on it) to 1 (at the rim).</summary>
    protected static float Depth(double distance, double radius) => (float)Math.Min(distance / (radius + 0.5), 1);
}
