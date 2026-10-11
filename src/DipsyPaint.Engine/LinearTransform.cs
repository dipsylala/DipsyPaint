namespace DipsyPaint.Engine;

/// <summary>
/// A linear map of the plane about a centre, in document coordinates (y down): the point at offset
/// (x, y) from the centre goes to offset (M11·x + M12·y, M21·x + M22·y). Reference Paint's free
/// rotation and skew. Entries are rounded to 12 decimal places, so exact angles (90°, 180°) give
/// exact maps and every platform's maths library gives the same pixels.
/// </summary>
public readonly record struct LinearTransform(double M11, double M12, double M21, double M22)
{
    /// <summary>Skew angles must be strictly between −90° and 90°; reference Paint accepts −89° to 89°.</summary>
    public const int MaxSkewDegrees = 89;

    public static LinearTransform Identity => new(1, 0, 0, 1);

    /// <summary>Whether this is the identity, give or take rounding (e.g. turning by an angle and back).</summary>
    public bool IsIdentity =>
        Math.Abs(M11 - 1) < 1e-9 && Math.Abs(M12) < 1e-9 && Math.Abs(M21) < 1e-9 && Math.Abs(M22 - 1) < 1e-9;

    /// <summary>Rotation by <paramref name="degrees"/>, clockwise on screen (as Rotate right 90°).</summary>
    public static LinearTransform Rotation(double degrees)
    {
        var radians = Math.IEEERemainder(degrees, 360) * Math.PI / 180;
        double cos = Snap(Math.Cos(radians)), sin = Snap(Math.Sin(radians));
        return new LinearTransform(cos, -sin, sin, cos);
    }

    /// <summary>
    /// Reference Paint's skew: first horizontal, then vertical. A positive horizontal angle shifts the
    /// top to the right of the bottom (upright edges lean right, like italics); a positive vertical
    /// angle raises the right side above the left. Angles must be within ±<see cref="MaxSkewDegrees"/>.
    /// </summary>
    public static LinearTransform Skew(double horizontalDegrees, double verticalDegrees)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(horizontalDegrees), MaxSkewDegrees, nameof(horizontalDegrees));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(verticalDegrees), MaxSkewDegrees, nameof(verticalDegrees));
        double h = Snap(Math.Tan(horizontalDegrees * Math.PI / 180)), v = Snap(Math.Tan(verticalDegrees * Math.PI / 180));
        // x' = x − h·y, then y'' = y − v·x'.
        return new LinearTransform(1, -h, -v, Snap(1 + v * h));
    }

    /// <summary>A scale along the axes, applied after the map so far (stretching what is shown).</summary>
    public static LinearTransform Scale(double x, double y) => new(x, 0, 0, y);

    /// <summary>This map followed by <paramref name="next"/>.</summary>
    public LinearTransform Then(LinearTransform next) => new(
        Snap(next.M11 * M11 + next.M12 * M21), Snap(next.M11 * M12 + next.M12 * M22),
        Snap(next.M21 * M11 + next.M22 * M21), Snap(next.M21 * M12 + next.M22 * M22));

    public (double X, double Y) Apply(double x, double y) => (M11 * x + M12 * y, M21 * x + M22 * y);

    /// <summary>Applies the map to <paramref name="point"/> about (<paramref name="centreX"/>, <paramref name="centreY"/>).</summary>
    public (double X, double Y) Apply((double X, double Y) point, double centreX, double centreY)
    {
        var (x, y) = Apply(point.X - centreX, point.Y - centreY);
        return (centreX + x, centreY + y);
    }

    public LinearTransform Inverse()
    {
        var determinant = M11 * M22 - M12 * M21;
        if (Math.Abs(determinant) < 1e-12)
        {
            throw new InvalidOperationException("The transform collapses the plane and has no inverse.");
        }

        return new LinearTransform(Snap(M22 / determinant), Snap(-M12 / determinant), Snap(-M21 / determinant), Snap(M11 / determinant));
    }

    /// <summary>
    /// The size, in whole pixels, of the axis-aligned box around a <paramref name="width"/>×<paramref name="height"/>
    /// rectangle once mapped (at least 1×1).
    /// </summary>
    public (int Width, int Height) SizeOf(int width, int height) =>
        (Extent(Math.Abs(M11) * width + Math.Abs(M12) * height), Extent(Math.Abs(M21) * width + Math.Abs(M22) * height));

    // A hair under a whole number counts as that number, so rounding noise never adds a pixel.
    private static int Extent(double size) => (int)Math.Clamp(Math.Ceiling(size - 1e-6), 1, int.MaxValue);

    private static double Snap(double value) => Math.Round(value, 12) + 0.0; // + 0.0 turns −0 into 0
}

/// <summary>Maps whole bitmaps through a <see cref="LinearTransform"/>.</summary>
internal static class PixelWarp
{
    /// <summary>
    /// Maps a <paramref name="width"/>×<paramref name="height"/> bitmap (row-major, premultiplied) through
    /// <paramref name="transform"/> into a new <paramref name="newWidth"/>×<paramref name="newHeight"/> one
    /// with the same centre. Each new pixel is sampled bilinearly at its centre's preimage; samples off
    /// the source read as <paramref name="background"/> (transparent by default), which also softens the
    /// edges. A map that sends pixel centres onto pixel centres (a quarter turn, the identity) copies exactly.
    /// </summary>
    public static Rgba[] Warp(
        Rgba[] source, int width, int height, LinearTransform transform, int newWidth, int newHeight, Rgba background = default)
    {
        var result = new Rgba[newWidth * newHeight];
        ForEachRow(width, height, transform, newWidth, newHeight, (y, sx, sy, stepX, stepY) =>
        {
            for (var x = 0; x < newWidth; x++)
            {
                result[y * newWidth + x] = Sample(source, width, height, sx + x * stepX, sy + x * stepY, background);
            }
        });
        return result;
    }

    /// <summary>Warps a shape mask as <see cref="Warp(Rgba[], int, int, LinearTransform, int, int, Rgba)"/> does pixels, keeping pixels at least half covered.</summary>
    public static bool[] Warp(bool[] mask, int width, int height, LinearTransform transform, int newWidth, int newHeight)
    {
        var result = new bool[newWidth * newHeight];
        ForEachRow(width, height, transform, newWidth, newHeight, (y, sx, sy, stepX, stepY) =>
        {
            for (var x = 0; x < newWidth; x++)
            {
                var (x0, fx) = Split(sx + x * stepX);
                var (y0, fy) = Split(sy + x * stepY);
                bool At(int i, int j) => i >= 0 && j >= 0 && i < width && j < height && mask[j * width + i];
                var coverage = (At(x0, y0) ? (1 - fx) * (1 - fy) : 0) + (At(x0 + 1, y0) ? fx * (1 - fy) : 0)
                    + (At(x0, y0 + 1) ? (1 - fx) * fy : 0) + (At(x0 + 1, y0 + 1) ? fx * fy : 0);
                result[y * newWidth + x] = coverage * 255 >= 127.5; // as an opaque pixel's alpha rounded to 128 or more
            }
        });
        return result;
    }

    // Calls `row` for every row y of the new bitmap (in parallel for large ones; each pixel is computed
    // on its own, so the result does not depend on the order) with the preimage of the centre of its
    // first pixel, in source pixel-centre coordinates (pixel i's centre at i), and the step per column.
    private static void ForEachRow(
        int width, int height, LinearTransform transform, int newWidth, int newHeight, Action<int, double, double, double, double> row)
    {
        var inverse = transform.Inverse();
        double centreX = width / 2.0 - 0.5, centreY = height / 2.0 - 0.5;
        double dx = 0.5 - newWidth / 2.0, newCentreY = newHeight / 2.0;
        void Row(int y)
        {
            var dy = y + 0.5 - newCentreY;
            row(y, inverse.M11 * dx + inverse.M12 * dy + centreX, inverse.M21 * dx + inverse.M22 * dy + centreY, inverse.M11, inverse.M21);
        }

        if (newWidth * (long)newHeight < 1 << 16)
        {
            for (var y = 0; y < newHeight; y++)
            {
                Row(y);
            }
        }
        else
        {
            Parallel.For(0, newHeight, Row);
        }
    }

    private static Rgba Sample(Rgba[] source, int width, int height, double sx, double sy, Rgba background)
    {
        var (x0, fx) = Split(sx);
        var (y0, fy) = Split(sy);
        if (x0 < -1 || y0 < -1 || x0 >= width || y0 >= height)
        {
            return background;
        }

        Rgba At(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? source[y * width + x] : background;

        if (fx == 0 && fy == 0)
        {
            return At(x0, y0);
        }

        double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
        Rgba p00 = At(x0, y0), p10 = At(x0 + 1, y0), p01 = At(x0, y0 + 1), p11 = At(x0 + 1, y0 + 1);
        byte Mix(byte a, byte b, byte c, byte d) =>
            (byte)Math.Clamp((int)Math.Round(a * w00 + b * w10 + c * w01 + d * w11), 0, 255);
        return new Rgba(
            Mix(p00.R, p10.R, p01.R, p11.R), Mix(p00.G, p10.G, p01.G, p11.G),
            Mix(p00.B, p10.B, p01.B, p11.B), Mix(p00.A, p10.A, p01.A, p11.A));
    }

    // The whole and fractional parts of a coordinate; within a hair of a whole number it is that number.
    private static (int Whole, double Fraction) Split(double value)
    {
        var whole = Math.Floor(value);
        var fraction = value - whole;
        if (fraction > 1 - 1e-9)
        {
            (whole, fraction) = (whole + 1, 0);
        }
        else if (fraction < 1e-9)
        {
            fraction = 0;
        }

        return ((int)Math.Clamp(whole, int.MinValue / 2, int.MaxValue / 2), fraction);
    }
}
