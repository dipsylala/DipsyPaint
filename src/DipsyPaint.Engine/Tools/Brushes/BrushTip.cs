namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// How an artistic brush marks pixels: the coverage its tip leaves along a path, and its texture
/// over a filled area. Tips are stateless and render into a <see cref="Stroke"/>, so the brush
/// tools and textured shape outlines and fills can share them. Points are in pixel coordinates,
/// where pixel (i, j) has its centre at (i + 0.5, j + 0.5). Textures are keyed on the stroke
/// target's pixel coordinates, like the grain of the paper: they are deterministic and line up
/// between strokes.
/// </summary>
public abstract class BrushTip
{
    /// <summary>A flat nib held at 45°, rising to the right (/): thick on strokes across it, thin along it.</summary>
    public static BrushTip CalligraphyBrush { get; } = new CalligraphyTip(rising: true);

    /// <summary>A flat nib held at 45°, falling to the right (\).</summary>
    public static BrushTip CalligraphyPen { get; } = new CalligraphyTip(rising: false);

    /// <summary>Wax on rough paper: coarse grain with gaps and a ragged edge.</summary>
    public static BrushTip Crayon { get; } = new CrayonTip();

    /// <summary>Translucent, even ink with a round tip.</summary>
    public static BrushTip Marker { get; } = new MarkerTip();

    /// <summary>Graphite: fine grain, denser at the centre of the tip.</summary>
    public static BrushTip NaturalPencil { get; } = new NaturalPencilTip();

    /// <summary>Covers the mark the tip leaves moving from (x0, y0) to (x1, y1) at <paramref name="size"/> pixels.</summary>
    public abstract void Segment(Stroke stroke, double x0, double y0, double x1, double y1, double size);

    /// <summary>
    /// Covers the mark the tip leaves moving from (x0, y0) to (x1, y1) while its size changes evenly
    /// from <paramref name="startSize"/> to <paramref name="endSize"/> pixels, as pen pressure varies.
    /// A tip that cannot taper uses the larger size.
    /// </summary>
    public virtual void Segment(Stroke stroke, double x0, double y0, double x1, double y1, double startSize, double endSize) =>
        Segment(stroke, x0, y0, x1, y1, Math.Max(startSize, endSize));

    /// <summary>Covers the mark the tip leaves along <paramref name="points"/>; a single point leaves one dab.</summary>
    public void Polyline(Stroke stroke, ReadOnlySpan<(double X, double Y)> points, double size)
    {
        if (points.Length == 1)
        {
            Segment(stroke, points[0].X, points[0].Y, points[0].X, points[0].Y, size);
        }

        for (var i = 1; i < points.Length; i++)
        {
            Segment(stroke, points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, size);
        }
    }

    /// <summary>
    /// Covers pixel (x, y) of a filled area that covers <paramref name="coverage"/> (0–1) of it,
    /// with the tip's texture.
    /// </summary>
    public abstract void Fill(Stroke stroke, int x, int y, float coverage);

    /// <summary>
    /// The coverage (0–1) the tip's texture leaves on pixel (x, y) deep inside a filled area: what
    /// <see cref="Fill"/> covers there at full coverage. Unlike <see cref="Fill"/>, it needs no
    /// <see cref="Stroke"/>, so large areas can be computed in parallel rows.
    /// </summary>
    public virtual float Interior(int x, int y) => 1f;
}
