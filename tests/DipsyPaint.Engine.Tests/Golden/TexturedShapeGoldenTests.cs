using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Shapes outlined and filled with each textured style (ticket 37). Outline is red (primary
/// colour), fill blue (secondary colour).
/// <list type="bullet">
/// <item>Top row, outline only: a rectangle at 3 px, an oval at 5 px and a five-point star at 8 px;
/// then a heart outlined and filled with the style at 5 px.</item>
/// <item>Middle row: a rectangle filled with the style; an oval with a solid 3 px outline and the
/// style's fill; a rectangle with the style's 8 px outline and a solid fill; an oval outlined and
/// filled with the style at 1 px, drawn with the right button (colours swapped).</item>
/// <item>Bottom row, open shapes: lines at 8 and 3 px, a curve at 5 px, and a polygon outlined and
/// filled with the style at 3 px.</item>
/// </list>
/// Textures are keyed on pixel coordinates and seeded from where each shape starts, so the images
/// are the same on every platform.
/// </summary>
public class TexturedShapeGoldenTests
{
    private static readonly Rgba Red = new(220, 30, 30, 255);
    private static readonly Rgba Blue = new(30, 60, 200, 255);

    [Theory]
    [InlineData(ShapeStyle.Crayon, "shape-textured-crayon")]
    [InlineData(ShapeStyle.Marker, "shape-textured-marker")]
    [InlineData(ShapeStyle.Oil, "shape-textured-oil")]
    [InlineData(ShapeStyle.NaturalPencil, "shape-textured-natural-pencil")]
    [InlineData(ShapeStyle.Watercolour, "shape-textured-watercolour")]
    public void TexturedStyles(ShapeStyle style, string golden)
    {
        var settings = new ShapeSettings();
        var editor = new DocumentEditor(Document.CreateBlank(160, 124))
        {
            ActiveTool = new ShapeTool(settings),
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };

        void Use(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, int size) =>
            (settings.Kind, settings.Outline, settings.Fill, settings.Size) = (kind, outline, fill, size);

        void Draw(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, int size, (double X, double Y) from, (double X, double Y) to,
            PointerButton button = PointerButton.Left)
        {
            Use(kind, outline, fill, size);
            Script.Stroke(editor, button, from, to);
            editor.CommitPending();
        }

        Draw(ShapeKind.Rectangle, style, ShapeStyle.None, 3, (4.5, 4.5), (34.5, 36.5));
        Draw(ShapeKind.Ellipse, style, ShapeStyle.None, 5, (40.5, 4.5), (76.5, 36.5));
        Draw(ShapeKind.FivePointStar, style, ShapeStyle.None, 8, (82.5, 2.5), (118.5, 37.5));
        Draw(ShapeKind.Heart, style, style, 5, (124.5, 4.5), (156.5, 36.5));

        Draw(ShapeKind.Rectangle, ShapeStyle.None, style, 1, (4.5, 42.5), (34.5, 74.5));
        Draw(ShapeKind.Ellipse, ShapeStyle.Solid, style, 3, (40.5, 42.5), (76.5, 74.5));
        Draw(ShapeKind.Rectangle, style, ShapeStyle.Solid, 8, (82.5, 42.5), (118.5, 74.5));
        Draw(ShapeKind.Ellipse, style, style, 1, (156.5, 74.5), (124.5, 42.5), PointerButton.Right);

        Draw(ShapeKind.Line, style, ShapeStyle.None, 8, (8.5, 84.5), (40.5, 116.5));
        Draw(ShapeKind.Line, style, ShapeStyle.None, 3, (8.5, 118.5), (50.5, 92.5));

        Use(ShapeKind.Curve, style, ShapeStyle.None, 5);
        Script.Stroke(editor, PointerButton.Left, (56.5, 104.5), (104.5, 104.5));
        Script.Stroke(editor, PointerButton.Left, (70.5, 70.5));
        Script.Stroke(editor, PointerButton.Left, (92.5, 138.5));
        editor.CommitPending();

        Use(ShapeKind.Polygon, style, style, 3);
        Script.Stroke(editor, PointerButton.Left, (112.5, 120.5), (154.5, 120.5));
        Script.Stroke(editor, PointerButton.Left, (140.5, 82.5));
        Script.Stroke(editor, PointerButton.Left, (114.5, 94.5));
        editor.CommitPending();

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }
}
