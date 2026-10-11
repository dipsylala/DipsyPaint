using System.Buffers;

namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Renders shapes as hard-edged (aliased) pixels, as reference Paint's solid shapes, into a
/// transparent buffer for the <see cref="Overlay"/>. Box shapes fill the frame given by two corner
/// pixels, with the outline growing inwards: a pixel is inside when its centre is, and outline
/// when its centre is also nearer the edge than the outline size. Lines and curves run through
/// pixel centres.
/// </summary>
internal static class ShapeRasterizer
{
    /// <summary>The rows in each band of a large outline marked in parallel.</summary>
    private const int Band = 64;

    /// <summary>
    /// A line between two pixels, or a box shape filling the frame they span. A box shape is rendered
    /// into <paramref name="buffer"/> when it is long enough (see <see cref="Buffer"/>).
    /// </summary>
    public static (IntRect Bounds, Rgba[] Pixels) Render(
        ShapeKind kind, (int X, int Y) a, (int X, int Y) b, ShapeStyle outlineStyle, ShapeStyle fillStyle, int size,
        Rgba outline, Rgba fill, Rgba[]? buffer = null)
    {
        size = Math.Max(size, 1);
        if (kind == ShapeKind.Line)
        {
            return outlineStyle == ShapeStyle.None ? (IntRect.Empty, []) : Polyline([a, b], size, outline);
        }

        var bounds = Adornment.FrameOf(a, b);
        var thickness = outlineStyle == ShapeStyle.None ? 0 : size;
        var fillPaint = fillStyle == ShapeStyle.None ? Rgba.Transparent : fill;
        if (kind is not (ShapeKind.Rectangle or ShapeKind.Ellipse))
        {
            var contours = ShapeOutlines.Of(kind, bounds.Width, bounds.Height)
                .Select(c => c.Select(p => (bounds.X + p.X, bounds.Y + p.Y)).ToArray())
                .ToList();
            return (bounds, Closed(bounds, contours, thickness, outline, fillPaint, buffer));
        }

        // Pixel x of a row and its mirror image (width - 1 - x) are the same part, and each part's
        // pixels lie nearer the middle of the row than any pixel outside it. So a row is runs:
        // transparent, outline, fill, outline, transparent, and only their ends are searched for.
        var (width, height) = (bounds.Width, bounds.Height);
        var half = (width + 1) / 2;
        var pixels = Buffer(width * height, buffer);
        ForEachRow(width * height, height, () => 0, (y, _) =>
        {
            Part At(int x) => kind == ShapeKind.Rectangle
                ? RectanglePart(x, y, width, height, thickness)
                : EllipsePart(x, y, width, height, thickness);
            var outer = First(half, x => At(x) != Part.Outside);
            var inner = Math.Max(outer, First(half, x => At(x) == Part.Fill));
            var row = pixels.AsSpan(y * width, width);
            row[..outer].Clear();
            row[(width - outer)..].Clear();
            row[outer..inner].Fill(outline);
            row[(width - inner)..(width - outer)].Fill(outline);
            if (width - inner > inner)
            {
                row[inner..(width - inner)].Fill(fillPaint);
            }
        });

        return (bounds, pixels);
    }

    /// <summary>
    /// Closed polygons (in document coordinates, pixel edges at whole numbers) within
    /// <paramref name="bounds"/>: even-odd inside, with an outline of <paramref name="thickness"/>
    /// (0 for none) along the inside of every edge; rendered into <paramref name="buffer"/> when it
    /// is long enough (see <see cref="Buffer"/>).
    /// </summary>
    public static Rgba[] Closed(
        IntRect bounds, IReadOnlyList<(double X, double Y)[]> contours, int thickness, Rgba outline, Rgba fill, Rgba[]? buffer = null)
    {
        var inside = Inside(bounds, contours);
        var edge = Edge(bounds, contours, thickness);
        var pixels = Buffer(bounds.Width * bounds.Height, buffer);
        ForEachRow(bounds.Width * bounds.Height, bounds.Height, () => 0, (y, _) =>
        {
            for (var i = y * bounds.Width; i < (y + 1) * bounds.Width; i++)
            {
                pixels[i] = !inside[i] ? Rgba.Transparent : edge[i] ? outline : fill;
            }
        });

        ReturnMask(inside);
        ReturnMask(edge);
        return pixels;
    }

    /// <summary>
    /// The pixels of <paramref name="bounds"/> (row-major) whose centre is inside the closed
    /// polygons, even-odd; see <see cref="Closed"/>. The mask is rented (see <see cref="ReturnMask"/>).
    /// </summary>
    public static bool[] Inside(IntRect bounds, IReadOnlyList<(double X, double Y)[]> contours)
    {
        var inside = RentMask(bounds.Width * bounds.Height);
        // Rows are independent, so a large shape (e.g. one being turned) is filled in parallel rows.
        ForEachRow(bounds.Width * bounds.Height, bounds.Height, () => new List<double>(), (y, crossings) =>
        {
            inside.AsSpan(y * bounds.Width, bounds.Width).Clear();
            var cy = bounds.Y + y + 0.5;
            crossings.Clear();
            foreach (var contour in contours)
            {
                for (var i = 0; i < contour.Length; i++)
                {
                    var (p, q) = (contour[i], contour[(i + 1) % contour.Length]);
                    if ((p.Y <= cy) != (q.Y <= cy))
                    {
                        crossings.Add(p.X + (cy - p.Y) * (q.X - p.X) / (q.Y - p.Y));
                    }
                }
            }

            crossings.Sort();
            for (var k = 0; k + 1 < crossings.Count; k += 2)
            {
                // Pixels whose centre lies in [crossings[k], crossings[k + 1]).
                var from = Math.Max(0, (int)Math.Ceiling(crossings[k] - bounds.X - 0.5));
                var to = Math.Min(bounds.Width, (int)Math.Ceiling(crossings[k + 1] - bounds.X - 0.5));
                Array.Fill(inside, true, y * bounds.Width + from, Math.Max(0, to - from));
            }
        });

        return inside;
    }

    /// <summary>
    /// The pixels of <paramref name="bounds"/> (row-major) whose centre is nearer than
    /// <paramref name="thickness"/> to an edge of the closed polygons (none for 0): the outline
    /// band of <see cref="Closed"/> where it is inside. The mask is rented (see <see cref="ReturnMask"/>).
    /// </summary>
    public static bool[] Edge(IntRect bounds, IReadOnlyList<(double X, double Y)[]> contours, int thickness)
    {
        // An edge through pixel centres (e.g. an arrow's shaft at three quarters of an even height)
        // leaves the pixels beside it exactly a whole number of pixels away; counting those keeps the
        // outline `thickness` pixels wide there. Edges along pixel edges are unaffected.
        var reach = thickness + 1e-9;
        var edge = RentMask(bounds.Width * bounds.Height);

        // Marks only add, so a large shape is marked in parallel bands of rows, each marking its own
        // rows near the edges that reach it.
        var bands = bounds.Width * bounds.Height < 1 << 16 ? 1 : (bounds.Height + Band - 1) / Band;
        Parallel.For(0, bands, band =>
        {
            var (from, to) = bands == 1 ? (0, bounds.Height) : (band * Band, Math.Min(bounds.Height, (band + 1) * Band));
            edge.AsSpan(from * bounds.Width, (to - from) * bounds.Width).Clear();
            if (thickness <= 0)
            {
                return;
            }

            foreach (var contour in contours)
            {
                for (var i = 0; i < contour.Length; i++)
                {
                    var (p, q) = (contour[i], contour[(i + 1) % contour.Length]);
                    if (Math.Max(p.Y, q.Y) + reach + 1 >= bounds.Y + from && Math.Min(p.Y, q.Y) - reach - 1 < bounds.Y + to)
                    {
                        MarkNear(edge, bounds, p.X, p.Y, q.X, q.Y, reach, from, to);
                    }
                }
            }
        });

        return edge;
    }

    /// <summary>
    /// A pixel array of at least <paramref name="length"/> pixels to render into: <paramref name="reuse"/>
    /// if it is long enough, else a new one, with room to grow if it replaces one. Either way it may
    /// hold anything, so the caller writes every pixel. Reusing the array of an earlier rendering
    /// saves allocating, clearing and collecting one on every pointer move of a drag (issue #102).
    /// </summary>
    public static Rgba[] Buffer(int length, Rgba[]? reuse) =>
        reuse is null ? GC.AllocateUninitializedArray<Rgba>(length)
        : reuse.Length >= length ? reuse
        : GC.AllocateUninitializedArray<Rgba>((int)Math.Min(length * 5L / 4, Array.MaxLength));

    /// <summary>
    /// Gives back a mask that <see cref="Inside"/> or <see cref="Edge"/> rented, once it is no longer
    /// used. Masks are rented because a large shape redrawn on every pointer move of a drag would
    /// otherwise allocate megabytes per move (issue #102).
    /// </summary>
    public static void ReturnMask(bool[] mask) => ArrayPool<bool>.Shared.Return(mask);

    // A mask of at least `length` pixels; it may hold anything.
    private static bool[] RentMask(int length) => ArrayPool<bool>.Shared.Rent(length);

    // Calls `row` for rows 0..height of a bitmap of `size` pixels, in parallel when it is large, with
    // a scratch value from `scratch` per thread.
    public static void ForEachRow<T>(int size, int height, Func<T> scratch, Action<int, T> row)
    {
        if (size < 1 << 16)
        {
            var value = scratch();
            for (var y = 0; y < height; y++)
            {
                row(y, value);
            }

            return;
        }

        Parallel.For(0, height, scratch, (y, _, value) =>
        {
            row(y, value);
            return value;
        }, _ => { });
    }

    /// <summary>
    /// A line through pixels (pixel centres; fractional points allowed), <paramref name="size"/>
    /// wide with round ends and joins; 1 px wide it is a Bresenham line between the rounded points.
    /// </summary>
    public static (IntRect Bounds, Rgba[] Pixels) Polyline(IReadOnlyList<(double X, double Y)> points, int size, Rgba color)
    {
        size = Math.Max(size, 1);
        var margin = size / 2 + 1;
        int minX = (int)Math.Floor(points.Min(p => p.X)), maxX = (int)Math.Ceiling(points.Max(p => p.X));
        int minY = (int)Math.Floor(points.Min(p => p.Y)), maxY = (int)Math.Ceiling(points.Max(p => p.Y));
        var bounds = new IntRect(minX - margin, minY - margin, maxX - minX + 1 + 2 * margin, maxY - minY + 1 + 2 * margin);
        var pixels = new Rgba[bounds.Width * bounds.Height];
        if (size == 1)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var from = Round(points[Math.Max(0, i - 1)]);
                Bresenham(from, Round(points[i]), (x, y) => pixels[(y - bounds.Y) * bounds.Width + x - bounds.X] = color);
            }

            return (bounds, pixels);
        }

        // Every pixel whose centre lies within size/2 of the line. An even width centres on the
        // pixel corner so that it covers exactly `size` rows or columns.
        var offset = size % 2 == 0 ? 1.0 : 0.5;
        var near = new bool[pixels.Length];
        for (var i = 0; i < points.Count; i++)
        {
            var (p, q) = (points[Math.Max(0, i - 1)], points[i]);
            MarkNear(near, bounds, p.X + offset, p.Y + offset, q.X + offset, q.Y + offset, size / 2.0, 0, bounds.Height);
        }

        for (var i = 0; i < pixels.Length; i++)
        {
            if (near[i])
            {
                pixels[i] = color;
            }
        }

        return (bounds, pixels);
    }

    /// <summary>The pixel nearest a point given in pixel-centre coordinates.</summary>
    public static (int X, int Y) Round((double X, double Y) point) =>
        ((int)Math.Floor(point.X + 0.5), (int)Math.Floor(point.Y + 0.5));

    private enum Part
    {
        Outside,
        Outline,
        Fill,
    }

    // The first of 0 to count - 1 for which `holds` is true, where it is false before that and true
    // after; count if there is none.
    private static int First(int count, Func<int, bool> holds)
    {
        int low = 0, high = count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (holds(middle))
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    private static Part RectanglePart(int x, int y, int width, int height, int thickness) =>
        x < thickness || y < thickness || x >= width - thickness || y >= height - thickness ? Part.Outline : Part.Fill;

    // A pixel belongs to an ellipse when its centre does; the outline is the band between the
    // ellipse inscribed in the frame and the one inset by the outline thickness.
    private static Part EllipsePart(int x, int y, int width, int height, int thickness)
    {
        double rx = width / 2.0, ry = height / 2.0;
        double dx = x + 0.5 - rx, dy = y + 0.5 - ry;
        if (!Inside(dx, dy, rx, ry))
        {
            return Part.Outside;
        }

        return Inside(dx, dy, rx - thickness, ry - thickness) ? Part.Fill : Part.Outline;
    }

    private static bool Inside(double dx, double dy, double rx, double ry) =>
        rx > 0 && ry > 0 && (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1;

    /// <summary>
    /// Marks the pixels of <paramref name="bounds"/>, in its rows <paramref name="fromRow"/> to
    /// <paramref name="toRow"/> (exclusive), whose centre is nearer than <paramref name="radius"/> to
    /// the segment, visiting only the rows and columns it can reach.
    /// </summary>
    private static void MarkNear(bool[] mask, IntRect bounds, double x0, double y0, double x1, double y1, double radius, int fromRow, int toRow)
    {
        var radiusSquared = radius * radius;
        var top = Math.Max(fromRow, (int)Math.Floor(Math.Min(y0, y1) - radius - bounds.Y) - 1);
        var bottom = Math.Min(toRow - 1, (int)Math.Ceiling(Math.Max(y0, y1) + radius - bounds.Y) + 1);
        for (var y = top; y <= bottom; y++)
        {
            var cy = bounds.Y + y + 0.5;

            // Only the part of the segment within `radius` of this row can be near its pixels.
            double t0 = 0, t1 = 1, dy = y1 - y0;
            if (dy != 0)
            {
                double ta = (cy - radius - y0) / dy, tb = (cy + radius - y0) / dy;
                t0 = Math.Max(0, Math.Min(ta, tb));
                t1 = Math.Min(1, Math.Max(ta, tb));
                if (t0 > t1)
                {
                    continue;
                }
            }
            else if (Math.Abs(y0 - cy) > radius)
            {
                continue;
            }

            double xa = x0 + t0 * (x1 - x0), xb = x0 + t1 * (x1 - x0);
            var left = Math.Max(0, (int)Math.Floor(Math.Min(xa, xb) - radius - bounds.X) - 1);
            var right = Math.Min(bounds.Width - 1, (int)Math.Ceiling(Math.Max(xa, xb) + radius - bounds.X) + 1);
            for (var x = left; x <= right; x++)
            {
                if (DistanceSquared(bounds.X + x + 0.5, cy, x0, y0, x1, y1) < radiusSquared)
                {
                    mask[y * bounds.Width + x] = true;
                }
            }
        }
    }

    private static double DistanceSquared(double px, double py, double x0, double y0, double x1, double y1)
    {
        double dx = x1 - x0, dy = y1 - y0;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((px - x0) * dx + (py - y0) * dy) / lengthSquared, 0, 1);
        double ex = px - (x0 + t * dx), ey = py - (y0 + t * dy);
        return ex * ex + ey * ey;
    }

    private static void Bresenham((int X, int Y) from, (int X, int Y) to, Action<int, int> plot)
    {
        var (x0, y0) = from;
        int dx = Math.Abs(to.X - x0), dy = -Math.Abs(to.Y - y0);
        int stepX = x0 < to.X ? 1 : -1, stepY = y0 < to.Y ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            plot(x0, y0);
            if (x0 == to.X && y0 == to.Y)
            {
                return;
            }

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += stepY;
            }
        }
    }
}
