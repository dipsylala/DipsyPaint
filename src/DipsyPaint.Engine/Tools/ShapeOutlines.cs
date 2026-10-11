namespace DipsyPaint.Engine.Tools;

/// <summary>
/// The outlines of the box shapes: every <see cref="ShapeKind"/> except line, curve and polygon,
/// which are drawn through points the user places. An outline fills a frame of the given size at
/// the origin (y down); curved parts are flattened into short straight sides. Proportions follow
/// the Office shapes that reference Paint's gallery is drawn from (e.g. a rounded rectangle's
/// corner radius is a sixth of its shorter side). Only arithmetic is used, no trigonometry, so
/// every platform rasterises the same pixels.
/// </summary>
public static class ShapeOutlines
{
    private const double Kappa = 0.5522847498307936; // control distance of a quarter-circle Bézier
    private const double Sqrt3Half = 0.8660254037844386;

    // cos and sin of 5°, for arcs built by repeated rotation.
    private const double Cos5 = 0.9961946980917455;
    private const double Sin5 = 0.08715574274765817;

    /// <summary>
    /// The closed contours of <paramref name="kind"/> in a <paramref name="width"/>×<paramref name="height"/>
    /// frame. Most shapes have one; the cloud callout has three (its cloud and two bubbles).
    /// </summary>
    public static IReadOnlyList<(double X, double Y)[]> Of(ShapeKind kind, double width, double height)
    {
        double w = width, h = height, n = Math.Min(w, h);
        var segments = Math.Clamp((int)(Math.Max(w, h) / 3), 6, 64);
        return kind switch
        {
            ShapeKind.Rectangle => [[(0, 0), (w, 0), (w, h), (0, h)]],
            ShapeKind.Ellipse => [Ellipse(w / 2, h / 2, w / 2, h / 2)],
            ShapeKind.RoundedRectangle => [RoundedRectangle(w, h, h, 0, 0, segments)],
            ShapeKind.Triangle => [[(w / 2, 0), (w, h), (0, h)]],
            ShapeKind.RightTriangle => [[(0, 0), (w, h), (0, h)]],
            ShapeKind.Diamond => [[(w / 2, 0), (w, h / 2), (w / 2, h), (0, h / 2)]],
            ShapeKind.Pentagon => [Fit(PentagonVertices, w, h)],
            ShapeKind.Hexagon => [[(n / 4, 0), (w - n / 4, 0), (w, h / 2), (w - n / 4, h), (n / 4, h), (0, h / 2)]],
            ShapeKind.RightArrow => [RightArrow(w, h)],
            ShapeKind.LeftArrow => [RightArrow(w, h).Select(p => (w - p.X, p.Y)).ToArray()],
            ShapeKind.DownArrow => [RightArrow(h, w).Select(p => (p.Y, p.X)).ToArray()],
            ShapeKind.UpArrow => [RightArrow(h, w).Select(p => (p.Y, h - p.X)).ToArray()],
            ShapeKind.FourPointStar => [Fit(FourPointStar(), w, h)],
            ShapeKind.FivePointStar => [Fit(FivePointStar(), w, h)],
            ShapeKind.SixPointStar => [Fit(SixPointStar(), w, h)],
            ShapeKind.RoundedRectangularCallout => [RoundedRectangle(w, h * 0.8, h, 0.4 * w, 0.2 * w, segments)],
            ShapeKind.OvalCallout => [OvalCallout(w, h)],
            ShapeKind.CloudCallout => CloudCallout(w, h, segments),
            ShapeKind.Heart => [Fit(Heart(segments), w, h)],
            ShapeKind.Lightning => [Fit(Lightning, w, h)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a box shape."),
        };
    }

    /// <summary>Points along a cubic Bézier, from <paramref name="p0"/> to <paramref name="p3"/> inclusive.</summary>
    public static (double X, double Y)[] FlattenCubic(
        (double X, double Y) p0, (double X, double Y) c1, (double X, double Y) c2, (double X, double Y) p3, int segments)
    {
        var points = new (double X, double Y)[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments;
            var u = 1 - t;
            double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
            points[i] = (a * p0.X + b * c1.X + c * c2.X + d * p3.X, a * p0.Y + b * c1.Y + c * c2.Y + d * p3.Y);
        }

        return points;
    }

    // Maps the bounding box of the points onto [0, w] × [0, h].
    private static (double X, double Y)[] Fit((double X, double Y)[] points, double w, double h)
    {
        double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        return points.Select(p => ((p.X - minX) / (maxX - minX) * w, (p.Y - minY) / (maxY - minY) * h)).ToArray();
    }

    // An ellipse by 5° steps, starting at the top.
    private static (double X, double Y)[] Ellipse(double cx, double cy, double rx, double ry) =>
        Arc(cx, cy, rx, ry, 0, -1, 71);

    // Points at angles start, start + 5°, … (steps + 1 points), the start given as its cos and sin.
    private static (double X, double Y)[] Arc(double cx, double cy, double rx, double ry, double cos, double sin, int steps)
    {
        var points = new (double X, double Y)[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            points[i] = (cx + rx * cos, cy + ry * sin);
            (cos, sin) = (cos * Cos5 - sin * Sin5, sin * Cos5 + cos * Sin5);
        }

        return points;
    }

    /// <summary>
    /// A rectangle <paramref name="w"/> × <paramref name="bodyHeight"/> with rounded corners; when
    /// <paramref name="frameHeight"/> is taller, a tail from the bottom edge (between
    /// <paramref name="tailRight"/> and <paramref name="tailLeft"/>) to a point at the frame's
    /// bottom left, as reference Paint's rounded rectangular callout.
    /// </summary>
    private static (double X, double Y)[] RoundedRectangle(
        double w, double bodyHeight, double frameHeight, double tailRight, double tailLeft, int segments)
    {
        double b = bodyHeight, r = Math.Min(w, b) / 6;
        var points = new List<(double X, double Y)>();
        void Corner((double X, double Y) from, (double X, double Y) corner, (double X, double Y) to) =>
            points.AddRange(FlattenCubic(
                from,
                (from.X + (corner.X - from.X) * Kappa, from.Y + (corner.Y - from.Y) * Kappa),
                (to.X + (corner.X - to.X) * Kappa, to.Y + (corner.Y - to.Y) * Kappa),
                to,
                Math.Max(2, segments / 4)));

        Corner((r, 0), (0, 0), (0, r));
        Corner((0, b - r), (0, b), (r, b));
        if (frameHeight > b)
        {
            points.AddRange([(tailLeft, b), (w / 10, frameHeight), (tailRight, b)]);
        }

        Corner((w - r, b), (w, b), (w, b - r));
        Corner((w, r), (w, 0), (w - r, 0));
        return [.. points];
    }

    // Shaft half the frame's height; head as long as half its shorter side.
    private static (double X, double Y)[] RightArrow(double w, double h)
    {
        var neck = w - Math.Min(w, h) / 2;
        return [(0, h / 4), (neck, h / 4), (neck, 0), (w, h / 2), (neck, h), (neck, h * 3 / 4), (0, h * 3 / 4)];
    }

    // Unit-circle points, clockwise from the top.
    private static readonly (double X, double Y)[] PentagonVertices =
    [
        (0, -1), (0.9510565162951535, -0.3090169943749474), (0.5877852522924731, 0.8090169943749475),
        (-0.5877852522924731, 0.8090169943749475), (-0.9510565162951535, -0.3090169943749474),
    ];

    // Points alternate with inner corners at 35% of the radius (the Office shape's 25% leaves
    // points too thin to survive pixel-centre sampling at small sizes).
    private static (double X, double Y)[] FourPointStar()
    {
        const double c = 0.35 * 0.7071067811865476;
        return [(0, -1), (c, -c), (1, 0), (c, c), (0, 1), (-c, c), (-1, 0), (-c, -c)];
    }

    // A regular five-pointed star (a pentagram's outline).
    private static (double X, double Y)[] FivePointStar()
    {
        const double inner = 0.3819660112501051;
        (double X, double Y)[] innerDirections =
        [
            (0.5877852522924731, -0.8090169943749475), (0.9510565162951535, 0.3090169943749474), (0, 1),
            (-0.9510565162951535, 0.3090169943749474), (-0.5877852522924731, -0.8090169943749475),
        ];
        return Enumerable.Range(0, 5)
            .SelectMany(i => new[] { PentagonVertices[i], (innerDirections[i].X * inner, innerDirections[i].Y * inner) })
            .ToArray();
    }

    // A regular six-pointed star (a hexagram's outline).
    private static (double X, double Y)[] SixPointStar()
    {
        const double inner = 0.5773502691896258;
        (double X, double Y)[] outer = [(0, -1), (Sqrt3Half, -0.5), (Sqrt3Half, 0.5), (0, 1), (-Sqrt3Half, 0.5), (-Sqrt3Half, -0.5)];
        (double X, double Y)[] innerDirections = [(0.5, -Sqrt3Half), (1, 0), (0.5, Sqrt3Half), (-0.5, Sqrt3Half), (-1, 0), (-0.5, -Sqrt3Half)];
        return Enumerable.Range(0, 6)
            .SelectMany(i => new[] { outer[i], (innerDirections[i].X * inner, innerDirections[i].Y * inner) })
            .ToArray();
    }

    // An ellipse over the top 80% with a tail from its lower left (100° to 135°) to the bottom left.
    private static (double X, double Y)[] OvalCallout(double w, double h)
    {
        double cx = w / 2, cy = h * 0.4, rx = w / 2, ry = h * 0.4;
        // From 135° round through the top to 100° (y down, so 90° is the bottom).
        var body = Arc(cx, cy, rx, ry, -0.7071067811865476, 0.7071067811865476, 65);
        return [.. body, (w / 10, h)];
    }

    // A scalloped cloud over the top 78%, and two bubbles trailing to the bottom left.
    private static IReadOnlyList<(double X, double Y)[]> CloudCallout(double w, double h, int segments)
    {
        const double cos36 = 0.8090169943749475, sin36 = 0.5877852522924731;
        double[] bulge = [0.40, 0.30, 0.45, 0.35, 0.42, 0.30, 0.40, 0.36, 0.44, 0.32];
        var cloud = new List<(double X, double Y)>();
        (double X, double Y) from = (0, -1);
        foreach (var d in bulge)
        {
            (double X, double Y) to = (from.X * cos36 - from.Y * sin36, from.Y * cos36 + from.X * sin36);
            var bump = FlattenCubic(from, (from.X * (1 + d), from.Y * (1 + d)), (to.X * (1 + d), to.Y * (1 + d)), to, Math.Max(3, segments / 6));
            cloud.AddRange(bump.SkipLast(1));
            from = to;
        }

        return
        [
            Fit([.. cloud], w, h * 0.78),
            Arc(w * 0.22, h * 0.86, w * 0.07, h * 0.045, 0, -1, 71),
            Arc(w * 0.10, h * 0.96, w * 0.04, h * 0.04, 0, -1, 71),
        ];
    }

    private static (double X, double Y)[] Heart(int segments)
    {
        (double X, double Y)[][] curves =
        [
            [(0.5, 0.22), (0.42, 0.02), (0, 0), (0, 0.3)],
            [(0, 0.3), (0, 0.6), (0.38, 0.78), (0.5, 1)],
            [(0.5, 1), (0.62, 0.78), (1, 0.6), (1, 0.3)],
            [(1, 0.3), (1, 0), (0.58, 0.02), (0.5, 0.22)],
        ];
        return curves.SelectMany(c => FlattenCubic(c[0], c[1], c[2], c[3], Math.Max(3, segments / 2)).SkipLast(1)).ToArray();
    }

    // The Office lightning bolt, on its 21600-unit grid.
    private static readonly (double X, double Y)[] Lightning =
    [
        (8458, 0), (0, 3923), (7564, 8416), (4993, 9720), (12197, 13904), (9987, 14934), (21600, 21600),
        (14768, 12911), (16558, 12016), (11030, 6840), (12352, 6086),
    ];
}
