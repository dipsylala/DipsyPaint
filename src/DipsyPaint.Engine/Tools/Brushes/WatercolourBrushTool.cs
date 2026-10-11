namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Reference Paint's watercolour brush: a translucent wash with soft streaks, a little darker at
/// its edges, that builds up where the stroke goes back over itself (see <see cref="WatercolourTip"/>).
/// </summary>
public sealed class WatercolourBrushTool(StrokeSettings settings) : TexturedBrushTool(settings)
{
    protected override TexturedTip CreateTip(double radius, ulong seed) => new WatercolourTip(radius, seed);

    // Once the tip has fully left a pixel (one diameter of path, plus a margin), covering it again builds up.
    protected override double NewPassAfter(double radius) => 2 * radius + 2;
}

/// <summary>
/// Watercolour: a translucent wash with soft, broad streaks along the path and a fine paper grain,
/// where pigment pools at the edge, which is soft and slightly uneven.
/// </summary>
public sealed class WatercolourTip(double radius, ulong seed) : TexturedTip(radius, seed)
{
    private readonly double _streakWidth = Math.Max(1.5, radius / 3);
    private readonly double _streakLength = 3 * radius + 16;
    private readonly double _edgeLength = radius + 6;
    private readonly double _rim = Math.Max(1, 0.2 * radius);

    public override double Softness => 1.5;

    public override double Reach(double along, int side) =>
        Radius * (1 - 0.08 * ArtisticNoise.Smooth(Seed + (ulong)(side + 2), along / _edgeLength));

    public override double Texture(double across, double along, double depth)
    {
        var streak = ArtisticNoise.Smooth(Seed, across / _streakWidth, along / _streakLength);
        var grain = ArtisticNoise.Smooth(Seed ^ 0x27d4eb2fUL, across * 0.8, along * 0.8);
        var wash = 0.28 + 0.2 * streak + 0.1 * (grain - 0.5);
        var pooled = depth < _rim ? 0.25 * (1 - depth / _rim) : 0;
        return Math.Clamp(wash + pooled, 0, 1);
    }
}
