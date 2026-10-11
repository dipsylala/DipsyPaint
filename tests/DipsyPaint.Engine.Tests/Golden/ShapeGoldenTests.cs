using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Golden images for every shape, with the outline sizes reference Paint offers (1, 3, 5, 8 px).
/// Outline is red (primary colour), fill blue (secondary colour).
/// </summary>
public class ShapeGoldenTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static (DocumentEditor Editor, ShapeSettings Settings) CreateEditor(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, int width, int height)
    {
        var settings = new ShapeSettings { Kind = kind, Outline = outline, Fill = fill };
        var editor = new DocumentEditor(Document.CreateBlank(width, height))
        {
            ActiveTool = new ShapeTool(settings),
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };
        return (editor, settings);
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle, ShapeStyle.Solid, ShapeStyle.None, "shape-rectangle-outline")]
    [InlineData(ShapeKind.Rectangle, ShapeStyle.None, ShapeStyle.Solid, "shape-rectangle-fill")]
    [InlineData(ShapeKind.Rectangle, ShapeStyle.Solid, ShapeStyle.Solid, "shape-rectangle-outline-fill")]
    [InlineData(ShapeKind.Ellipse, ShapeStyle.Solid, ShapeStyle.None, "shape-ellipse-outline")]
    [InlineData(ShapeKind.Ellipse, ShapeStyle.None, ShapeStyle.Solid, "shape-ellipse-fill")]
    [InlineData(ShapeKind.Ellipse, ShapeStyle.Solid, ShapeStyle.Solid, "shape-ellipse-outline-fill")]
    public void BoxShapes(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, string golden)
    {
        var (editor, settings) = CreateEditor(kind, outline, fill, 88, 40);

        for (var i = 0; i < ShapeSettings.Sizes.Count; i++)
        {
            settings.Size = ShapeSettings.Sizes[i];
            var x = 2 + i * 22;
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 2.5), (x + 18.5, 22.5));  // tall
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 37.5), (x + 18.5, 26.5)); // wide, drawn upwards
            editor.CommitPending();
        }

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }

    [Fact]
    public void Lines()
    {
        var (editor, settings) = CreateEditor(ShapeKind.Line, ShapeStyle.Solid, ShapeStyle.None, 116, 52);

        for (var i = 0; i < ShapeSettings.Sizes.Count; i++)
        {
            settings.Size = ShapeSettings.Sizes[i];
            var x = 5 + i * 28;
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 4.5), (x + 16.5, 4.5));    // horizontal
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 12.5), (x + 5.5, 46.5));   // steep
            Script.Stroke(editor, PointerButton.Left, (x + 12.5, 12.5), (x + 20.5, 20.5)); // 45°
            Script.Stroke(editor, PointerButton.Left, (x + 20.5, 46.5), (x + 11.5, 30.5)); // drawn upwards
            editor.CommitPending();
        }

        GoldenImage.AssertMatches("shape-lines", editor.Compositor);
    }

    /// <summary>
    /// The shapes added with the full gallery (ticket 24). Top row: outline only, 1/3/5/8 px.
    /// Middle row: outline and fill, 1/3/5/8 px. Bottom row: fill only; a wide one drawn upwards
    /// with the right button (colours swapped), 1 px; a tall one, 3 px.
    /// </summary>
    [Theory]
    [InlineData(ShapeKind.RoundedRectangle, "shape-rounded-rectangle")]
    [InlineData(ShapeKind.Triangle, "shape-triangle")]
    [InlineData(ShapeKind.RightTriangle, "shape-right-triangle")]
    [InlineData(ShapeKind.Diamond, "shape-diamond")]
    [InlineData(ShapeKind.Pentagon, "shape-pentagon")]
    [InlineData(ShapeKind.Hexagon, "shape-hexagon")]
    [InlineData(ShapeKind.RightArrow, "shape-right-arrow")]
    [InlineData(ShapeKind.LeftArrow, "shape-left-arrow")]
    [InlineData(ShapeKind.UpArrow, "shape-up-arrow")]
    [InlineData(ShapeKind.DownArrow, "shape-down-arrow")]
    [InlineData(ShapeKind.FourPointStar, "shape-four-point-star")]
    [InlineData(ShapeKind.FivePointStar, "shape-five-point-star")]
    [InlineData(ShapeKind.SixPointStar, "shape-six-point-star")]
    [InlineData(ShapeKind.RoundedRectangularCallout, "shape-rounded-rectangular-callout")]
    [InlineData(ShapeKind.OvalCallout, "shape-oval-callout")]
    [InlineData(ShapeKind.CloudCallout, "shape-cloud-callout")]
    [InlineData(ShapeKind.Heart, "shape-heart")]
    [InlineData(ShapeKind.Lightning, "shape-lightning")]
    public void GalleryShapes(ShapeKind kind, string golden)
    {
        var (editor, settings) = CreateEditor(kind, ShapeStyle.Solid, ShapeStyle.None, 138, 118);

        void Draw(ShapeStyle outline, ShapeStyle fill, int size, PointerButton button, (double X, double Y) from, (double X, double Y) to)
        {
            (settings.Outline, settings.Fill, settings.Size) = (outline, fill, size);
            Script.Stroke(editor, button, from, to);
            editor.CommitPending();
        }

        for (var i = 0; i < ShapeSettings.Sizes.Count; i++)
        {
            var x = 2 + i * 34;
            Draw(ShapeStyle.Solid, ShapeStyle.None, ShapeSettings.Sizes[i], PointerButton.Left, (x + 0.5, 2.5), (x + 29.5, 31.5));
            Draw(ShapeStyle.Solid, ShapeStyle.Solid, ShapeSettings.Sizes[i], PointerButton.Left, (x + 0.5, 36.5), (x + 29.5, 65.5));
        }

        Draw(ShapeStyle.None, ShapeStyle.Solid, 1, PointerButton.Left, (2.5, 70.5), (31.5, 99.5));
        Draw(ShapeStyle.Solid, ShapeStyle.Solid, 1, PointerButton.Right, (99.5, 93.5), (36.5, 70.5));
        Draw(ShapeStyle.Solid, ShapeStyle.Solid, 3, PointerButton.Left, (104.5, 70.5), (133.5, 115.5));

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }

    /// <summary>
    /// Curves at 1/3/5/8 px: top, one bend then Enter; bottom, two bends (an S).
    /// </summary>
    [Fact]
    public void Curves()
    {
        var (editor, settings) = CreateEditor(ShapeKind.Curve, ShapeStyle.Solid, ShapeStyle.None, 140, 104);

        for (var i = 0; i < ShapeSettings.Sizes.Count; i++)
        {
            settings.Size = ShapeSettings.Sizes[i];
            var x = 6 + i * 34;
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 30.5), (x + 24.5, 30.5));
            Script.Stroke(editor, PointerButton.Left, (x + 12.5, 4.5));
            editor.CommitPending();

            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 70.5), (x + 24.5, 70.5));
            Script.Stroke(editor, PointerButton.Left, (x + 12.5, 60.5), (x + 2.5, 40.5)); // the first bend, dragged
            Script.Stroke(editor, PointerButton.Left, (x + 22.5, 100.5));
            editor.CommitPending();
        }

        GoldenImage.AssertMatches("shape-curves", editor.Compositor);
    }

    /// <summary>
    /// Polygons at 1/3/5/8 px: top, a triangle outline closed near its first corner; bottom, a
    /// notched outline-and-fill polygon closed by a double-click, its last side drawn with Shift.
    /// </summary>
    [Fact]
    public void Polygons()
    {
        var (editor, settings) = CreateEditor(ShapeKind.Polygon, ShapeStyle.Solid, ShapeStyle.None, 164, 78);

        for (var i = 0; i < ShapeSettings.Sizes.Count; i++)
        {
            settings.Size = ShapeSettings.Sizes[i];
            var x = 4 + i * 40;
            (settings.Outline, settings.Fill) = (ShapeStyle.Solid, ShapeStyle.None);
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 34.5), (x + 32.5, 34.5));
            Script.Stroke(editor, PointerButton.Left, (x + 16.5, 2.5));
            Script.Stroke(editor, PointerButton.Left, (x + 1.5, 33.5));
            editor.CommitPending();

            (settings.Outline, settings.Fill) = (ShapeStyle.Solid, ShapeStyle.Solid);
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 40.5), (x + 32.5, 40.5));
            Script.Stroke(editor, PointerButton.Left, (x + 20.5, 56.5));
            Script.Stroke(editor, PointerButton.Left, (x + 32.5, 74.5));
            editor.Apply(new PointerInput(PointerAction.Down, x + 0.5, 75.5, Shift: true));
            editor.Apply(new PointerInput(PointerAction.Up, x + 0.5, 75.5, Shift: true));
            Script.Stroke(editor, PointerButton.Left, (x + 0.5, 74.5));
            editor.CommitPending();
        }

        GoldenImage.AssertMatches("shape-polygons", editor.Compositor);
    }

    [Fact]
    public void AdjustedShape_MatchesTheShapeDrawnInPlace()
    {
        var (adjusted, _) = CreateEditor(ShapeKind.Ellipse, ShapeStyle.Solid, ShapeStyle.Solid, 88, 40);
        Script.Stroke(adjusted, PointerButton.Left, (10.5, 10.5), (30.5, 20.5));
        Script.Stroke(adjusted, PointerButton.Left, (20.5, 15.5), (40.5, 20.5)); // move
        Script.Stroke(adjusted, PointerButton.Left, (51, 26), (60, 36));          // resize by the bottom-right handle
        adjusted.CommitPending();

        var (direct, _) = CreateEditor(ShapeKind.Ellipse, ShapeStyle.Solid, ShapeStyle.Solid, 88, 40);
        Script.Stroke(direct, PointerButton.Left, (30.5, 15.5), (59.5, 35.5));
        direct.CommitPending();

        Assert.Equal(direct.Compositor.Pixels.ToArray(), adjusted.Compositor.Pixels.ToArray());
    }
}
