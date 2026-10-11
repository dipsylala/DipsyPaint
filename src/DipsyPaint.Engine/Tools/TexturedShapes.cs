using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Renders shapes whose outline or fill has a textured <see cref="ShapeStyle"/> (crayon, marker,
/// oil, natural pencil or watercolour) with the matching artistic brush, into a transparent buffer
/// for the <see cref="Overlay"/>, as <see cref="ShapeRasterizer"/> does for solid ones.
/// <list type="bullet">
/// <item>A textured outline is the brush, as wide as the outline size, swept along the shape's
/// path. On a closed shape the path is the outline fitted inside the frame inset by half the size,
/// so the stroke stays within the frame, as a solid outline does.</item>
/// <item>A textured fill spreads the brush's texture over the inside, up to the middle of a
/// textured outline, or up to a solid one. The outline is painted over it.</item>
/// </list>
/// The buffer is aligned with the document, so crayon, marker and natural pencil keep the paper
/// grain of their brush strokes. Oil and watercolour textures are seeded from the shape (see
/// <see cref="ArtisticNoise"/>). Everything is computed from coordinates and the seed only, so the
/// pixels are the same on every platform and however rows are split between threads.
/// </summary>
internal static class TexturedShapes
{
    /// <summary>The tip diameter whose texture a textured oil or watercolour fill spreads, in pixels.</summary>
    private const double FillTipSize = 20;

    /// <summary>The longest straight piece an outline is swept in, in pixels.</summary>
    private const double PieceLength = 32;

    public static bool IsTextured(ShapeStyle style) => style > ShapeStyle.Solid;

    /// <summary>
    /// A line or curve through <paramref name="points"/> (pixels, as <see cref="ShapeRasterizer.Polyline"/>)
    /// painted in a textured <paramref name="style"/> <paramref name="size"/> pixels wide.
    /// </summary>
    public static (IntRect Bounds, Rgba[] Pixels) Open(
        IReadOnlyList<(double X, double Y)> points, ShapeStyle style, int size, Rgba color, ulong seed)
    {
        var margin = size / 2 + 2;
        int minX = (int)Math.Floor(points.Min(p => p.X)), maxX = (int)Math.Ceiling(points.Max(p => p.X));
        int minY = (int)Math.Floor(points.Min(p => p.Y)), maxY = (int)Math.Ceiling(points.Max(p => p.Y));
        var bounds = new IntRect(minX - margin, minY - margin, maxX - minX + 1 + 2 * margin, maxY - minY + 1 + 2 * margin);
        var canvas = new ShapeCanvas(bounds);
        (double X, double Y)[] centres = [.. points.Select(p => (p.X + 0.5, p.Y + 0.5))];
        Sweep(canvas, [centres], style, size, color, seed);
        return (bounds, canvas.Pixels);
    }

    /// <summary>
    /// A closed shape: the polygons <paramref name="contours"/> (document coordinates, pixel edges at
    /// whole numbers) within <paramref name="frame"/>, outlined and filled in the given styles, at
    /// least one of them textured. Solid parts match <see cref="ShapeRasterizer.Closed"/>.
    /// </summary>
    public static Rgba[] Closed(
        IntRect frame, IReadOnlyList<(double X, double Y)[]> contours, ShapeStyle outlineStyle, ShapeStyle fillStyle,
        int size, Rgba outline, Rgba fill, ulong seed)
    {
        var canvas = new ShapeCanvas(frame);
        var pixels = canvas.Pixels;
        var path = IsTextured(outlineStyle) ? Inset(frame, contours, size) : contours;
        var edge = outlineStyle == ShapeStyle.Solid ? ShapeRasterizer.Edge(frame, contours, size) : null;
        var inside = edge is not null || (fillStyle != ShapeStyle.None && !IsTextured(outlineStyle))
            ? ShapeRasterizer.Inside(frame, contours)
            : null;

        if (fillStyle != ShapeStyle.None)
        {
            // Up to a solid outline, or to the middle of a textured one.
            var area = IsTextured(outlineStyle) ? ShapeRasterizer.Inside(frame, path) : inside!;
            var coverage = Interior(fillStyle, frame, ArtisticNoise.Hash(seed));
            ShapeRasterizer.ForEachRow(pixels.Length, frame.Height, () => 0, (y, _) =>
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var i = y * frame.Width + x;
                    if (area[i] && edge?[i] != true)
                    {
                        pixels[i] = fill.WithOpacity(coverage(frame.X + x, frame.Y + y));
                    }
                }
            });
        }

        if (edge is not null)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (inside![i] && edge[i])
                {
                    pixels[i] = outline;
                }
            }
        }
        else if (IsTextured(outlineStyle))
        {
            // Painted over the fill, as a brush stroke over paint; closed, back to its start.
            Sweep(canvas, [.. path.Select(c => c.Append(c[0]).ToArray())], outlineStyle, size, outline, seed);
        }

        return pixels;
    }

    // The contours scaled about the frame's centre into the frame inset by half the outline size on every side.
    private static List<(double X, double Y)[]> Inset(IntRect frame, IReadOnlyList<(double X, double Y)[]> contours, int size)
    {
        double centreX = frame.X + frame.Width / 2.0, centreY = frame.Y + frame.Height / 2.0;
        double sx = Math.Max(0, frame.Width - size) / (double)frame.Width, sy = Math.Max(0, frame.Height - size) / (double)frame.Height;
        return [.. contours.Select(c => c.Select(p => (centreX + (p.X - centreX) * sx, centreY + (p.Y - centreY) * sy)).ToArray())];
    }

    // How much of document pixel (x, y) a fill of `style` over `bounds` covers (0–1). A brush tip's
    // is its interior texture, as BrushTip.Fill paints it. A textured tip's is what TexturedPath.Area
    // gives a pixel inside the area: its texture as if dragged across the area from left to right in
    // rows. Both are computed per pixel here, without a stroke, so rows can be filled in parallel.
    private static Func<int, int, float> Interior(ShapeStyle style, IntRect bounds, ulong seed)
    {
        if (style == ShapeStyle.Solid)
        {
            return (_, _) => 1f;
        }

        if (BrushTipOf(style) is { } brush)
        {
            return (x, y) => Math.Clamp(brush.Interior(x, y), 0f, 1f);
        }

        var tip = TexturedTipOf(style, FillTipSize / 2, seed);
        return (x, y) => Math.Clamp((float)tip.Texture(y + 0.5 - bounds.Y, x + 0.5 - bounds.X, double.PositiveInfinity), 0f, 1f);
    }

    // Sweeps the brush of a textured style along each path (pixel-centre coordinates), as one stroke,
    // over what the canvas already holds.
    private static void Sweep(ShapeCanvas canvas, IReadOnlyList<(double X, double Y)[]> paths, ShapeStyle style, int size, Rgba color, ulong seed)
    {
        // A tip visits every pixel of a segment's bounding box, so long sides (e.g. of a turned
        // rectangle) are swept in short pieces, which leave the same mark.
        paths = [.. paths.Select(Subdivide)];
        if (BrushTipOf(style) is { } brush)
        {
            var stroke = new Stroke(canvas, color);
            foreach (var path in paths)
            {
                brush.Polyline(stroke, path, size);
            }

            return;
        }

        var tip = TexturedTipOf(style, size / 2.0, seed);
        var passes = new PassStroke(canvas, color, 1f, double.PositiveInfinity);
        foreach (var path in paths)
        {
            TexturedPath.Polyline(passes, tip, path);
        }
    }

    // The path with every side longer than PieceLength split into equal pieces no longer than it.
    private static (double X, double Y)[] Subdivide((double X, double Y)[] path)
    {
        var points = new List<(double X, double Y)> { path[0] };
        for (var i = 1; i < path.Length; i++)
        {
            var ((x0, y0), (x1, y1)) = (path[i - 1], path[i]);
            var pieces = (int)Math.Ceiling(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / PieceLength);
            for (var k = 1; k < pieces; k++)
            {
                points.Add((x0 + (x1 - x0) * k / pieces, y0 + (y1 - y0) * k / pieces));
            }

            points.Add(path[i]);
        }

        return [.. points];
    }

    private static BrushTip? BrushTipOf(ShapeStyle style) => style switch
    {
        ShapeStyle.Crayon => BrushTip.Crayon,
        ShapeStyle.Marker => BrushTip.Marker,
        ShapeStyle.NaturalPencil => BrushTip.NaturalPencil,
        _ => null,
    };

    private static TexturedTip TexturedTipOf(ShapeStyle style, double radius, ulong seed) => style switch
    {
        ShapeStyle.Oil => new OilTip(radius, seed),
        ShapeStyle.Watercolour => new WatercolourTip(radius, seed),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a textured style."),
    };

    /// <summary>
    /// A transparent buffer over <see cref="Bounds"/> addressed in document pixels, so a brush's
    /// texture lines up with the paper. Pixels outside the bounds, or left of or above the canvas,
    /// are dropped.
    /// </summary>
    private sealed class ShapeCanvas(IntRect bounds) : IPixelAccess
    {
        public IntRect Bounds { get; } = bounds;

        public Rgba[] Pixels { get; } = new Rgba[bounds.Width * bounds.Height];

        public int Width => Math.Max(0, Bounds.Right);

        public int Height => Math.Max(0, Bounds.Bottom);

        public Rgba GetPixel(int x, int y) => Contains(x, y) ? Pixels[IndexOf(x, y)] : Rgba.Transparent;

        public void SetPixel(int x, int y, Rgba value)
        {
            if (Contains(x, y))
            {
                Pixels[IndexOf(x, y)] = value;
            }
        }

        public void ReadRegion(IntRect region, Span<Rgba> destination)
        {
            for (var y = 0; y < region.Height; y++)
            {
                for (var x = 0; x < region.Width; x++)
                {
                    destination[y * region.Width + x] = GetPixel(region.X + x, region.Y + y);
                }
            }
        }

        public void WriteRegion(IntRect region, ReadOnlySpan<Rgba> source)
        {
            for (var y = 0; y < region.Height; y++)
            {
                for (var x = 0; x < region.Width; x++)
                {
                    SetPixel(region.X + x, region.Y + y, source[y * region.Width + x]);
                }
            }
        }

        private bool Contains(int x, int y) => x >= Bounds.X && y >= Bounds.Y && x < Bounds.Right && y < Bounds.Bottom;

        private int IndexOf(int x, int y) => (y - Bounds.Y) * Bounds.Width + x - Bounds.X;
    }
}
