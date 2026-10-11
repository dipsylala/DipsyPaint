namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Crayon: wax that sticks only to the peaks of coarse paper grain, leaving speckled gaps. Towards
/// the edge of the tip less wax reaches the paper, so the edge is ragged rather than round.
/// </summary>
internal sealed class CrayonTip : RoundTip
{
    private const uint Seed = 0x85EBCA6B;

    protected override float Coverage(int x, int y, double distance, double radius)
    {
        var grain = 0.35f * PaperGrain.Smooth(x, y, 3, Seed)
                    + 0.35f * PaperGrain.Smooth(x, y, 2, Seed + 1)
                    + 0.3f * PaperGrain.Speckle(x, y, Seed + 2);
        var depth = Depth(distance, radius);
        var pressure = 1 - depth * depth * depth;
        var wax = Math.Clamp((grain + 0.45f * pressure - 0.72f) / 0.15f, 0f, 1f);
        return wax * Rim(distance, radius);
    }
}
