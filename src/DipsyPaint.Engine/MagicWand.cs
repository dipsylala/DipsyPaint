namespace DipsyPaint.Engine;

/// <summary>
/// The magic wand (an addition beyond reference Paint, ADR-0011): selects the contiguous area
/// (4-connected) of colours similar to a clicked pixel, found with the fill tool's scanline search
/// (<see cref="FloodRegion"/>, no recursion) on the active layer, which is what the fill tool samples.
/// </summary>
public static class MagicWand
{
    /// <summary>
    /// The largest difference allowed in each channel (premultiplied red, green, blue and alpha, 0–255)
    /// from the clicked pixel for a tolerance of <paramref name="percent"/> (0–100, clamped): 0% matches
    /// the exact colour only, 100% every colour.
    /// </summary>
    public static int ChannelTolerance(int percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) * 255 / 100.0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The area of <paramref name="pixels"/> around (<paramref name="x"/>, <paramref name="y"/>) within
    /// <paramref name="tolerancePercent"/> (see <see cref="ChannelTolerance"/>), as a selection not yet
    /// lifted, trimmed to its bounds; null when the point is outside <paramref name="pixels"/>.
    /// </summary>
    public static Selection? Select(IPixelAccess pixels, int x, int y, int tolerancePercent)
    {
        if ((uint)x >= (uint)pixels.Width || (uint)y >= (uint)pixels.Height)
        {
            return null;
        }

        var mask = new bool[pixels.Width * pixels.Height];
        foreach (var span in FloodRegion.Find(pixels, x, y, ChannelTolerance(tolerancePercent)))
        {
            Array.Fill(mask, true, span.Y * pixels.Width + span.X, span.Width);
        }

        return SelectionMasks.Trim(new IntRect(0, 0, pixels.Width, pixels.Height), mask);
    }
}
