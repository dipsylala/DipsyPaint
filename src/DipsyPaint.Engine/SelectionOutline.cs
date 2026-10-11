namespace DipsyPaint.Engine;

/// <summary>
/// Where a raster of screen pixels shows the document: the screen pixel whose centre is at (u, v) in the raster
/// (u = column + 0.5, v = row + 0.5) shows document point (<c>M11·u + M21·v + OffsetX</c>, <c>M12·u + M22·v + OffsetY</c>).
/// </summary>
public readonly record struct ScreenMap(double M11, double M12, double M21, double M22, double OffsetX, double OffsetY);

/// <summary>Row <paramref name="y"/> of a raster to draw into, as wide as the raster.</summary>
public delegate Span<Rgba> RasterRow(int y);

/// <summary>
/// The edge of a selection's shape (<see cref="Selection.Mask"/>) as the shell shows it, instead of the bounding
/// rectangle (issue #111): a dashed line one screen pixel wide just outside the selected pixels, around holes and
/// islands too. It is drawn in screen pixels, so its cost depends on the area drawn, not on how ragged the edge is
/// (a magic-wand selection of a noisy 4K image has millions of corners).
/// </summary>
public static class SelectionOutline
{
    /// <summary>Length of each black and each white dash, in screen pixels.</summary>
    public const int DashLength = 4;

    /// <summary>
    /// Draws the edge of the pixels of <paramref name="bounds"/> set in <paramref name="mask"/> (row-major,
    /// <paramref name="bounds"/>-sized; null for all) into a <paramref name="width"/>×<paramref name="height"/> raster of
    /// screen pixels (<paramref name="rows"/>, which may be asked for rows in parallel), which shows the document as
    /// <paramref name="map"/> says (each screen pixel showing the document pixel under its centre, as nearest-neighbour
    /// scaling does). A screen pixel is on the edge when it shows an unselected pixel and a screen pixel beside it, also
    /// diagonally, shows a selected one; it is then opaque black or white in dashes of <see cref="DashLength"/>: by
    /// column (counted from −<paramref name="dashX"/>), or by row (from −<paramref name="dashY"/>) where the edge runs
    /// down. Every other pixel becomes transparent.
    /// </summary>
    public static void Draw(
        IntRect bounds, bool[]? mask, ScreenMap map, int width, int height, RasterRow rows, int dashX = 0, int dashY = 0)
    {
        if (mask is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(mask.Length, bounds.Width * bounds.Height, nameof(mask));
        }

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var raster = new Raster(bounds, mask, map, width, dashX, dashY);
        // Bands of rows, each with its own three rows of samples; every pixel is computed on its own.
        var bands = Math.Clamp(height / 64, 1, Environment.ProcessorCount * 2);
        Parallel.For(0, bands, band => raster.DrawRows(height * band / bands, height * (band + 1) / bands, height, rows));
    }

    private sealed class Raster(IntRect bounds, bool[]? mask, ScreenMap map, int width, int dashX, int dashY)
    {
        private static readonly Rgba Black = new(0, 0, 0, 255);

        // Turned or skewed: each pixel is mapped on its own; else columns map alike on every row.
        private readonly bool _upright = map.M12 == 0 && map.M21 == 0;
        private readonly int[] _columns = Columns(bounds, map, width);

        public void DrawRows(int top, int bottom, int height, RasterRow rows)
        {
            // Samples of the rows above, at and below the one drawn, one pixel wider on each side (unselected).
            var above = new bool[width + 2];
            var row = new bool[width + 2];
            var below = new bool[width + 2];
            Sample(top - 1, height, above);
            Sample(top, height, row);
            for (var y = top; y < bottom; y++)
            {
                Sample(y + 1, height, below);
                var line = rows(y);
                for (var x = 0; x < width; x++)
                {
                    bool beside = row[x] | row[x + 2], overOrUnder = above[x + 1] | below[x + 1];
                    if (row[x + 1] || !(beside | overOrUnder | above[x] | above[x + 2] | below[x] | below[x + 2]))
                    {
                        line[x] = Rgba.Transparent;
                        continue;
                    }

                    // Dashes run down an edge that runs down (a selected pixel beside it only), else along.
                    var along = beside && !overOrUnder ? y + dashY : x + dashX;
                    line[x] = ((along >> 2) & 1) == 0 ? Black : Rgba.White; // >> 2: ÷ DashLength, rounded down
                }

                (above, row, below) = (row, below, above);
            }
        }

        // Whether each screen pixel of row y shows a selected pixel; rows outside the raster show none.
        private void Sample(int y, int height, bool[] samples)
        {
            Array.Clear(samples);
            if (y < 0 || y >= height)
            {
                return;
            }

            var v = y + 0.5;
            if (_upright)
            {
                var row = (int)Math.Floor(map.M22 * v + map.OffsetY) - bounds.Y;
                if ((uint)row >= (uint)bounds.Height)
                {
                    return;
                }

                for (var x = 0; x < width; x++)
                {
                    var column = _columns[x];
                    samples[x + 1] = column >= 0 && (mask is null || mask[row * bounds.Width + column]);
                }

                return;
            }

            for (var x = 0; x < width; x++)
            {
                var u = x + 0.5;
                var column = (int)Math.Floor(map.M11 * u + map.M21 * v + map.OffsetX) - bounds.X;
                var line = (int)Math.Floor(map.M12 * u + map.M22 * v + map.OffsetY) - bounds.Y;
                samples[x + 1] = (uint)column < (uint)bounds.Width && (uint)line < (uint)bounds.Height
                    && (mask is null || mask[line * bounds.Width + column]);
            }
        }

        // For an upright map, the column of the shape each screen column shows, or −1 outside it.
        private static int[] Columns(IntRect bounds, ScreenMap map, int width)
        {
            var columns = new int[width];
            for (var x = 0; x < width; x++)
            {
                var column = (int)Math.Floor(map.M11 * (x + 0.5) + map.OffsetX) - bounds.X;
                columns[x] = (uint)column < (uint)bounds.Width ? column : -1;
            }

            return columns;
        }
    }
}
