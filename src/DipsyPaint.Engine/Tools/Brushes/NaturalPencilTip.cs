namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Natural pencil: graphite catching on fine paper grain. The centre of the tip presses hardest,
/// so it fills more of the grain than the edge, and even the centre stays slightly grainy and grey.
/// </summary>
internal sealed class NaturalPencilTip : RoundTip
{
    private const uint Seed = 0x9E3779B9;

    protected override float Coverage(int x, int y, double distance, double radius)
    {
        var grain = 0.55f * PaperGrain.Smooth(x, y, 2, Seed) + 0.45f * PaperGrain.Speckle(x, y, Seed + 1);
        var depth = Depth(distance, radius);
        var pressure = 1 - depth * depth;
        var lead = Math.Clamp(0.9f * grain + 0.5f * pressure - 0.2f, 0f, 1f);
        return 0.8f * lead * Rim(distance, radius);
    }
}
