namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// The paper's grain: deterministic value noise over pixel coordinates (integer hashing and
/// basic float arithmetic only, so it is identical on every platform). Each value is 0–1.
/// </summary>
internal static class PaperGrain
{
    /// <summary>Noise that changes from pixel to pixel.</summary>
    public static float Speckle(int x, int y, uint seed) => Hash(x, y, seed) * (1f / 16777216f);

    /// <summary>Smooth noise with features about <paramref name="cell"/> pixels across.</summary>
    public static float Smooth(int x, int y, int cell, uint seed)
    {
        var gx = FloorDivide(x, cell);
        var gy = FloorDivide(y, cell);
        var fx = Fade((x - gx * cell + 0.5f) / cell);
        var fy = Fade((y - gy * cell + 0.5f) / cell);
        var top = Lerp(Speckle(gx, gy, seed), Speckle(gx + 1, gy, seed), fx);
        var bottom = Lerp(Speckle(gx, gy + 1, seed), Speckle(gx + 1, gy + 1, seed), fx);
        return Lerp(top, bottom, fy);
    }

    /// <summary>A 24-bit hash of a lattice point.</summary>
    private static uint Hash(int x, int y, uint seed)
    {
        var h = unchecked((uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ seed * 0xCB1AB31Fu);
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        return h >> 8;
    }

    private static int FloorDivide(int value, int divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

    private static float Fade(float t) => t * t * (3 - 2 * t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
