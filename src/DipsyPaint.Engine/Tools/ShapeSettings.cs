namespace DipsyPaint.Engine.Tools;

/// <summary>
/// The shapes in reference Paint's shape gallery. Line and curve are open (no fill); curve and
/// polygon are built over several gestures; the rest fill the frame dragged out for them.
/// </summary>
public enum ShapeKind
{
    Line,
    Rectangle,
    Ellipse,
    Curve,
    RoundedRectangle,
    Polygon,
    Triangle,
    RightTriangle,
    Diamond,
    Pentagon,
    Hexagon,
    RightArrow,
    LeftArrow,
    UpArrow,
    DownArrow,
    FourPointStar,
    FivePointStar,
    SixPointStar,
    RoundedRectangularCallout,
    OvalCallout,
    CloudCallout,
    Heart,
    Lightning,
}

/// <summary>
/// How a shape's outline or fill is painted. Reference Paint also offers crayon, marker, oil,
/// natural pencil and watercolour; those arrive with the artistic brushes (ticket 37) as further
/// members. The options panel lists the members in order.
/// </summary>
public enum ShapeStyle
{
    None,
    Solid,
}

/// <summary>
/// The shape tool's options, shared by its options panel and every <see cref="ShapeTool"/> it
/// creates. Outline, fill and size apply to the pending shape too (after
/// <see cref="DocumentEditor.RefreshPending"/>); <see cref="Kind"/> applies to the next shape.
/// </summary>
public sealed class ShapeSettings
{
    /// <summary>The outline sizes reference Paint offers for shapes, in pixels.</summary>
    public static IReadOnlyList<int> Sizes { get; } = [1, 3, 5, 8];

    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;

    /// <summary>The outline (for a line, the line itself); painted in the colour of the button that drew the shape.</summary>
    public ShapeStyle Outline { get; set; } = ShapeStyle.Solid;

    /// <summary>The interior, painted in the other colour. Lines and curves have none.</summary>
    public ShapeStyle Fill { get; set; } = ShapeStyle.None;

    /// <summary>Outline width in pixels.</summary>
    public int Size { get; set; } = 1;

    /// <summary>Whether <paramref name="kind"/> has an inside to fill (all but line and curve).</summary>
    public static bool CanFill(ShapeKind kind) => kind is not (ShapeKind.Line or ShapeKind.Curve);
}
