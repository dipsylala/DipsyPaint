namespace DipsyPaint.Engine.Tools;

public enum ShapeKind
{
    Line,
    Rectangle,
    Ellipse,
}

/// <summary>
/// How a shape's outline or fill is painted. Reference Paint also offers crayon, marker, oil,
/// natural pencil and watercolour; those arrive with the artistic brushes.
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

    /// <summary>The interior, painted in the other colour. Lines have none.</summary>
    public ShapeStyle Fill { get; set; } = ShapeStyle.None;

    /// <summary>Outline width in pixels.</summary>
    public int Size { get; set; } = 1;
}
