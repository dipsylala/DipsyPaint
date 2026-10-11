namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// A textured brush tip, such as oil paint or watercolour: how much paint it leaves at a point,
/// given where the point lies across and along the path. <see cref="TexturedPath"/> sweeps it along
/// a path or spreads its texture over an area. Its randomness comes from <see cref="Seed"/> only.
/// </summary>
public abstract class TexturedTip(double radius, ulong seed)
{
    /// <summary>Half the tip's width, in pixels.</summary>
    public double Radius { get; } = radius;

    protected ulong Seed { get; } = seed;

    /// <summary>Width, in pixels, of the antialiased rim at the tip's outline.</summary>
    public virtual double Softness => 1;

    /// <summary>
    /// How far the tip reaches from the path's centre line, on its left (<paramref name="side"/> −1)
    /// or right (+1), <paramref name="along"/> pixels along the path; at most <see cref="Radius"/>.
    /// A ragged edge varies it.
    /// </summary>
    public virtual double Reach(double along, int side) => Radius;

    /// <summary>
    /// Coverage (0–1) the paint leaves <paramref name="across"/> pixels from the centre line (signed)
    /// and <paramref name="along"/> pixels along the path, <paramref name="depth"/> pixels in from the
    /// side edge the tip reaches (infinite inside an area). The outline's antialiasing is applied on top.
    /// </summary>
    public abstract double Texture(double across, double along, double depth);
}
