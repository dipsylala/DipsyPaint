namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Marker: a round tip of even, translucent ink. A stroke that crosses itself does not darken, but
/// separate strokes layer up, as with a real marker.
/// </summary>
internal sealed class MarkerTip : RoundTip
{
    /// <summary>How much of the paper one coat of ink covers.</summary>
    private const float Ink = 0.6f;

    protected override float Coverage(int x, int y, double distance, double radius) => Ink * Rim(distance, radius);
}
