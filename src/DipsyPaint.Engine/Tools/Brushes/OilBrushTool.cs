namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Reference Paint's oil brush: thick, nearly opaque paint with bristle streaks along the path and
/// a ragged edge. Going over the same spot in one stroke does not build up (see <see cref="OilTip"/>).
/// </summary>
public sealed class OilBrushTool(StrokeSettings settings) : TexturedBrushTool(settings)
{
    protected override TexturedTip CreateTip(double radius, ulong seed) => new OilTip(radius, seed);

    protected override double NewPassAfter(double radius) => double.PositiveInfinity;
}

/// <summary>
/// Oil paint: solid where the bristles press, with lighter streaks running along the path where
/// they leave less paint and the canvas shows through, and an edge that wanders in and out.
/// </summary>
public sealed class OilTip(double radius, ulong seed) : TexturedTip(radius, seed)
{
    // Bristle streaks are this many pixels wide, and vary over this much path.
    private readonly double _bristle = Math.Max(1.0, radius / 10);
    private readonly double _streakLength = 2 * radius + 12;
    private readonly double _edgeLength = 0.6 * radius + 4;

    public override double Reach(double along, int side) =>
        Radius * (1 - 0.18 * ArtisticNoise.Smooth(Seed + (ulong)(side + 2), along / _edgeLength));

    public override double Texture(double across, double along, double depth)
    {
        var u = across / _bristle;
        var streak = 0.6 * ArtisticNoise.Smooth(Seed, u, along / _streakLength)
                     + 0.4 * ArtisticNoise.Smooth(Seed ^ 0x5bd1e995UL, 2.3 * u, along / (0.5 * _streakLength));
        // Most of the tip lays solid paint; the driest streaks let about half the canvas through.
        var dry = Math.Clamp((streak - 0.45) / 0.3, 0, 1);
        return 1 - 0.5 * dry * dry;
    }
}
