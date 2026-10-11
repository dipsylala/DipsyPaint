namespace DipsyPaint.Engine.Tools;

/// <summary>
/// How pen pressure shapes a stroke. Pressure-capable tools (the brushes) scale their tip's size by
/// <see cref="PointerInput.Pressure"/> at each event and taper the tip smoothly between events.
/// Full pressure (1, which mouse input and pressure-less devices report) leaves the size unchanged,
/// so those strokes are exactly as without pressure. A release keeps the pressure of the event
/// before it, because pens report no pressure as they lift off.
/// </summary>
public static class PenPressure
{
    /// <summary>
    /// The tip size, in pixels, for <paramref name="size"/> at <paramref name="pressure"/> (0–1):
    /// in proportion to the pressure, but never below <see cref="StrokeSettings.MinSize"/>.
    /// </summary>
    public static double ScaleSize(double size, float pressure) =>
        float.IsNaN(pressure) || pressure >= 1f ? size : Math.Max(StrokeSettings.MinSize, size * Math.Max(pressure, 0f));

    /// <summary>
    /// The point, as a fraction <c>t</c> (0–1) along the segment (<paramref name="dx"/>, <paramref name="dy"/>),
    /// of the disc that reaches furthest towards a point at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// from the segment's start, when the disc's radius grows by <paramref name="radiusChange"/> from start to end:
    /// where <c>radius(t) − distance(t)</c> is greatest. With no change it is the nearest point of the segment.
    /// </summary>
    public static double Nearest(double cx, double cy, double dx, double dy, double radiusChange)
    {
        var lengthSquared = dx * dx + dy * dy;
        if (radiusChange == 0)
        {
            return lengthSquared == 0 ? 0 : Math.Clamp((cx * dx + cy * dy) / lengthSquared, 0, 1);
        }

        // One end's disc contains the other's, so the larger end reaches furthest everywhere.
        if (radiusChange * radiusChange >= lengthSquared)
        {
            return radiusChange > 0 ? 1 : 0;
        }

        // radius(t) − distance(t) is concave in t; it peaks where the distance grows as fast as the
        // radius, a little past the nearest point towards the larger end.
        var nearest = (cx * dx + cy * dy) / lengthSquared;
        var offLine = Math.Abs(cx * dy - cy * dx) / Math.Sqrt(lengthSquared);
        var shift = offLine * radiusChange / Math.Sqrt(lengthSquared - radiusChange * radiusChange);
        return Math.Clamp(nearest + shift / Math.Sqrt(lengthSquared), 0, 1);
    }
}
