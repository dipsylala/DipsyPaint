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
    /// <summary>The longest straight piece an outline is swept in, in pixels.</summary>
    private const double PieceLength = 32;

    /// <summary>The rows in each band of a large outline swept in parallel.</summary>
    private const int SweepBand = 64;

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
        int size, Rgba outline, Rgba fill, ulong seed, FillTexture? texture = null, Rgba[]? buffer = null)
    {
        var canvas = new ShapeCanvas(frame, ShapeRasterizer.Buffer(frame.Width * frame.Height, buffer));
        var pixels = canvas.Pixels;
        var path = IsTextured(outlineStyle) ? Inset(frame, contours, size) : contours;
        var edge = outlineStyle == ShapeStyle.Solid ? ShapeRasterizer.Edge(frame, contours, size) : null;
        var inside = edge is not null || (fillStyle != ShapeStyle.None && !IsTextured(outlineStyle))
            ? ShapeRasterizer.Inside(frame, contours)
            : null;

        // Up to a solid outline, or to the middle of a textured one.
        var area = fillStyle == ShapeStyle.None ? null : IsTextured(outlineStyle) ? ShapeRasterizer.Inside(frame, path) : inside!;
        texture = IsTextured(fillStyle) ? FillTexture.For(fillStyle, seed, texture) : null;
        var (originX, originY) = texture?.OnPaper == false ? (0, 0) : (frame.X, frame.Y);
        // The fill colour at each scale the texture gives; a solid fill covers fully, which leaves it as it is.
        var colours = texture is null ? [] : Enumerable.Range(0, 256).Select(fill.Scaled).ToArray();
        var width = frame.Width;
        ShapeRasterizer.ForEachRow(width * frame.Height, frame.Height, () => edge is null ? [] : new bool[width], (y, scratch) =>
        {
            // The buffer may hold an earlier rendering, so every pixel is written.
            var row = pixels.AsSpan(y * width, width);
            row.Clear();
            if (area is not null)
            {
                ReadOnlySpan<bool> needed = area.AsSpan(y * width, width);
                if (edge is not null)
                {
                    for (var x = 0; x < width; x++)
                    {
                        scratch[x] = needed[x] && !edge[y * width + x];
                    }

                    needed = scratch;
                }

                if (texture is not null)
                {
                    texture.Paint(originX, originY + y, needed, colours, row);
                }
                else
                {
                    for (var x = 0; x < width; x++)
                    {
                        if (needed[x])
                        {
                            row[x] = fill;
                        }
                    }
                }
            }

            if (edge is not null)
            {
                for (var x = 0; x < width; x++)
                {
                    if (inside![y * width + x] && edge[y * width + x])
                    {
                        row[x] = outline;
                    }
                }
            }
        });

        foreach (var mask in new[] { edge, inside, area }.OfType<bool[]>().Distinct())
        {
            ShapeRasterizer.ReturnMask(mask);
        }

        if (IsTextured(outlineStyle))
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

    // Sweeps the brush of a textured style along each path (pixel-centre coordinates), as one stroke,
    // over what the canvas already holds.
    private static void Sweep(ShapeCanvas canvas, IReadOnlyList<(double X, double Y)[]> paths, ShapeStyle style, int size, Rgba color, ulong seed)
    {
        // A tip visits every pixel of a segment's bounding box, so long sides (e.g. of a turned
        // rectangle) are swept in short pieces, which leave the same mark.
        paths = [.. paths.Select(Subdivide)];
        var brush = BrushTipOf(style);
        var tip = brush is null ? TexturedTipOf(style, size / 2.0, seed) : null;

        // What a stroke leaves on a pixel depends only on the covers of that pixel, in order. So a
        // large canvas is swept in parallel bands of rows: each band sweeps, in order, the pieces that
        // reach it, and keeps only its own rows. The pixels are the same as from one sweep.
        var bounds = canvas.Bounds;
        var bands = bounds.Width * bounds.Height < 1 << 16 ? 1 : (bounds.Height + SweepBand - 1) / SweepBand;
        var reach = size / 2.0 + 1; // further than any tip reaches from its path
        // Each worker reuses one record of covered pixels for all its bands, rather than growing a new one per band (issue #115).
        Parallel.For(0, bands, () => new Touched(), (band, _, touched) =>
        {
            Paint(band, touched);
            return touched;
        }, _ => { });

        void Paint(int band, Touched touched)
        {
            var top = bands == 1 ? int.MinValue : bounds.Y + band * SweepBand;
            var bottom = bands == 1 ? int.MaxValue : top + SweepBand;
            var rows = new BandCanvas(canvas, top, bottom);
            bool Reaches((double X, double Y) p, (double X, double Y) q) =>
                Math.Max(p.Y, q.Y) + reach >= top && Math.Min(p.Y, q.Y) - reach < bottom;

            if (brush is not null)
            {
                var stroke = new Stroke(rows, color, touched.Covers) { Top = Math.Max(0, top) };
                foreach (var path in paths)
                {
                    // As BrushTip.Polyline.
                    if (path.Length == 1 && Reaches(path[0], path[0]))
                    {
                        brush.Segment(stroke, path[0].X, path[0].Y, path[0].X, path[0].Y, size);
                    }

                    for (var i = 1; i < path.Length; i++)
                    {
                        if (Reaches(path[i - 1], path[i]))
                        {
                            brush.Segment(stroke, path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y, size);
                        }
                    }
                }

                return;
            }

            var passes = new PassStroke(rows, color, 1f, double.PositiveInfinity, touched.Passes) { Top = Math.Max(0, top) };
            foreach (var path in paths)
            {
                // As TexturedPath.Polyline.
                if (path.Length == 1 && Reaches(path[0], path[0]))
                {
                    var (x, y) = path[0];
                    TexturedPath.Segment(passes, tip!, x, y, x, y, TextureFrame.ForSegment(x, y, x, y, 0));
                }

                var along = 0.0;
                for (var i = 1; i < path.Length; i++)
                {
                    var ((x0, y0), (x1, y1)) = (path[i - 1], path[i]);
                    if (Reaches(path[i - 1], path[i]))
                    {
                        TexturedPath.Segment(passes, tip!, x0, y0, x1, y1, TextureFrame.ForSegment(x0, y0, x1, y1, along));
                    }

                    along += Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                }
            }
        }
    }

    // A worker's records of covered pixels, reused for each band it sweeps.
    private sealed class Touched
    {
        public Dictionary<PixelKey, (Rgba Before, float Coverage)> Covers { get; } = [];

        public Dictionary<PixelKey, PassStroke.Entry> Passes { get; } = [];
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
    /// A buffer over <see cref="Bounds"/> addressed in document pixels, so a brush's texture lines
    /// up with the paper; transparent, unless given <c>pixels</c> to hold. Pixels outside the
    /// bounds, or left of or above the canvas, are dropped.
    /// </summary>
    private sealed class ShapeCanvas(IntRect bounds, Rgba[]? pixels = null) : IPixelAccess
    {
        public IntRect Bounds { get; } = bounds;

        public Rgba[] Pixels { get; } = pixels ?? new Rgba[bounds.Width * bounds.Height];

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

    /// <summary>
    /// The rows <paramref name="top"/> to <paramref name="bottom"/> (exclusive) of a <see cref="ShapeCanvas"/>:
    /// other rows read as transparent and drop what is written to them, so bands can be painted at once.
    /// </summary>
    private sealed class BandCanvas(ShapeCanvas canvas, int top, int bottom) : IPixelAccess
    {
        public int Width => canvas.Width;

        public int Height => Math.Min(canvas.Height, Math.Max(0, bottom));

        public Rgba GetPixel(int x, int y) => y >= top && y < bottom ? canvas.GetPixel(x, y) : Rgba.Transparent;

        public void SetPixel(int x, int y, Rgba value)
        {
            if (y >= top && y < bottom)
            {
                canvas.SetPixel(x, y, value);
            }
        }

        public void ReadRegion(IntRect region, Span<Rgba> destination)
        {
            for (var i = 0; i < region.Width * region.Height; i++)
            {
                destination[i] = GetPixel(region.X + i % region.Width, region.Y + i / region.Width);
            }
        }

        public void WriteRegion(IntRect region, ReadOnlySpan<Rgba> source)
        {
            for (var i = 0; i < region.Width * region.Height; i++)
            {
                SetPixel(region.X + i % region.Width, region.Y + i / region.Width, source[i]);
            }
        }
    }
}
