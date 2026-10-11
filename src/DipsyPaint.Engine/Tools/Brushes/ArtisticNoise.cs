namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Seeded pseudo-random numbers for sprayed and textured brushes. Integer hashing plus basic
/// floating-point arithmetic only (no trigonometry or library maths that may differ between C
/// runtimes), so the same seed gives the same pixels on Windows, macOS and Linux (ADR-0016).
/// </summary>
public static class ArtisticNoise
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;
    private const ulong Second = 0xD1B54A32D192ED03UL;
    private const double UnitScale = 1.0 / (1UL << 53);

    /// <summary>A seed for a stroke that starts at (x, y): the same start gives the same texture or spray.</summary>
    public static ulong Seed(double x, double y) =>
        Hash(((ulong)(long)Math.Floor(x) * Golden) ^ ((ulong)(long)Math.Floor(y) * Second));

    /// <summary>A well-mixed 64-bit hash (SplitMix64's finaliser).</summary>
    public static ulong Hash(ulong z)
    {
        z += Golden;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A number in [0, 1) for integer <paramref name="i"/> under <paramref name="seed"/>.</summary>
    public static double Unit(ulong seed, long i) => (Hash(seed ^ ((ulong)i * Golden)) >> 11) * UnitScale;

    /// <summary>A number in [0, 1) for the integer point (<paramref name="i"/>, <paramref name="j"/>) under <paramref name="seed"/>.</summary>
    public static double Unit(ulong seed, long i, long j) => Unit(Hash(seed ^ ((ulong)j * Second)), i);

    /// <summary>Smooth value noise in [0, 1) along one axis, with a new random value every whole unit.</summary>
    public static double Smooth(ulong seed, double x)
    {
        var cell = Math.Floor(x);
        var i = (long)cell;
        var a = Unit(seed, i);
        return a + (Unit(seed, i + 1) - a) * Fade(x - cell);
    }

    /// <summary>Smooth value noise in [0, 1) over a plane, with a new random value at every whole point.</summary>
    public static double Smooth(ulong seed, double x, double y)
    {
        double cellX = Math.Floor(x), cellY = Math.Floor(y);
        long i = (long)cellX, j = (long)cellY;
        double fx = Fade(x - cellX), fy = Fade(y - cellY);
        double topLeft = Unit(seed, i, j), bottomLeft = Unit(seed, i, j + 1);
        var top = topLeft + (Unit(seed, i + 1, j) - topLeft) * fx;
        var bottom = bottomLeft + (Unit(seed, i + 1, j + 1) - bottomLeft) * fx;
        return top + (bottom - top) * fy;
    }

    /// <summary>A generator of numbers in [0, 1) that continues a sequence under one seed.</summary>
    public struct Sequence(ulong seed)
    {
        private long _next;

        public double Next() => Unit(seed, _next++);
    }

    private static double Fade(double t) => t * t * (3 - 2 * t);
}
