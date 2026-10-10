namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Renders shapes as hard-edged (aliased) pixels, as reference Paint's solid shapes, into a
/// transparent buffer for the <see cref="Overlay"/>. Rectangles and ellipses fill the frame given
/// by two corner pixels, with the outline growing inwards; a line runs between two pixel centres.
/// </summary>
internal static class ShapeRasterizer
{
    public static (IntRect Bounds, Rgba[] Pixels) Render(
        ShapeKind kind, (int X, int Y) a, (int X, int Y) b, ShapeStyle outlineStyle, ShapeStyle fillStyle, int size,
        Rgba outline, Rgba fill)
    {
        size = Math.Max(size, 1);
        if (kind == ShapeKind.Line)
        {
            return outlineStyle == ShapeStyle.None ? (IntRect.Empty, []) : Line(a, b, size, outline);
        }

        var bounds = Adornment.FrameOf(a, b);
        var pixels = new Rgba[bounds.Width * bounds.Height];
        var thickness = outlineStyle == ShapeStyle.None ? 0 : size;
        var fillPaint = fillStyle == ShapeStyle.None ? Rgba.Transparent : fill;
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var part = kind == ShapeKind.Rectangle
                    ? RectanglePart(x, y, bounds.Width, bounds.Height, thickness)
                    : EllipsePart(x, y, bounds.Width, bounds.Height, thickness);
                pixels[y * bounds.Width + x] = part switch
                {
                    Part.Outline => outline,
                    Part.Fill => fillPaint,
                    _ => Rgba.Transparent,
                };
            }
        }

        return (bounds, pixels);
    }

    private enum Part
    {
        Outside,
        Outline,
        Fill,
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

    private static (IntRect Bounds, Rgba[] Pixels) Line((int X, int Y) a, (int X, int Y) b, int size, Rgba color)
    {
        var margin = size / 2 + 1;
        var frame = Adornment.FrameOf(a, b);
        var bounds = new IntRect(frame.X - margin, frame.Y - margin, frame.Width + 2 * margin, frame.Height + 2 * margin);
        var pixels = new Rgba[bounds.Width * bounds.Height];
        if (size == 1)
        {
            Bresenham(a, b, (x, y) => pixels[(y - bounds.Y) * bounds.Width + x - bounds.X] = color);
            return (bounds, pixels);
        }

        // Thick lines: every pixel whose centre lies within size/2 of the segment (round caps). An
        // even width centres on the pixel corner so that it covers exactly `size` rows or columns.
        var offset = size % 2 == 0 ? 1.0 : 0.5;
        double x0 = a.X + offset, y0 = a.Y + offset, x1 = b.X + offset, y1 = b.Y + offset;
        var radius = size / 2.0;
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                if (DistanceSquared(bounds.X + x + 0.5, bounds.Y + y + 0.5, x0, y0, x1, y1) < radius * radius)
                {
                    pixels[y * bounds.Width + x] = color;
                }
            }
        }

        return (bounds, pixels);
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
