namespace DipsyPaint.Engine;

/// <summary>
/// Finds the contiguous region of similar colour around a seed pixel, for the fill tool and the
/// magic wand. Pixels are 4-connected (no diagonal steps), so a 1 px diagonal line is a closed
/// boundary, as in reference Paint. Scanline algorithm with an explicit stack, so even a whole
/// 16384² layer cannot overflow the call stack (ADR-0018).
/// </summary>
public static class FloodRegion
{
    /// <summary>
    /// Returns the region containing (<paramref name="seedX"/>, <paramref name="seedY"/>) as
    /// non-overlapping one-row rectangles. A pixel belongs to the region when each of its
    /// channels (premultiplied) is within <paramref name="tolerance"/> of the seed pixel's
    /// (0 = exact match). The seed must lie on <paramref name="pixels"/>.
    /// </summary>
    public static List<IntRect> Find(IPixelAccess pixels, int seedX, int seedY, int tolerance = 0)
    {
        int width = pixels.Width, height = pixels.Height;
        if ((uint)seedX >= (uint)width || (uint)seedY >= (uint)height)
        {
            throw new ArgumentOutOfRangeException(nameof(seedX), $"Seed ({seedX}, {seedY}) is outside the {width}×{height} layer.");
        }

        var source = GC.AllocateUninitializedArray<Rgba>(width * height);
        pixels.ReadRegion(new IntRect(0, 0, width, height), source);
        var seed = source[seedY * width + seedX];
        var visited = new bool[source.Length];
        bool InRegion(int index) => !visited[index] && Similar(source[index], seed, tolerance);

        var spans = new List<IntRect>();
        var pending = new Stack<(int X, int Y)>();
        pending.Push((seedX, seedY));
        while (pending.TryPop(out var start))
        {
            var row = start.Y * width;
            if (!InRegion(row + start.X))
            {
                continue; // already reached from the row on its other side
            }

            // Grow the run left and right along its row.
            int left = start.X, right = start.X;
            while (left > 0 && InRegion(row + left - 1))
            {
                left--;
            }

            while (right < width - 1 && InRegion(row + right + 1))
            {
                right++;
            }

            Array.Fill(visited, true, row + left, right - left + 1);
            spans.Add(new IntRect(left, start.Y, right - left + 1, 1));

            // Queue one seed per run of region pixels directly above and below the run.
            foreach (var y in (ReadOnlySpan<int>)[start.Y - 1, start.Y + 1])
            {
                if ((uint)y >= (uint)height)
                {
                    continue;
                }

                var inRun = false;
                for (var x = left; x <= right; x++)
                {
                    var inside = InRegion(y * width + x);
                    if (inside && !inRun)
                    {
                        pending.Push((x, y));
                    }

                    inRun = inside;
                }
            }
        }

        return spans;
    }

    private static bool Similar(Rgba a, Rgba b, int tolerance) =>
        Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance
        && Math.Abs(a.B - b.B) <= tolerance && Math.Abs(a.A - b.A) <= tolerance;
}
