using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// One golden image per shape and outline/fill style, each with the outline sizes reference Paint
/// offers (1, 3, 5, 8 px). Outline is red (primary colour), fill blue (secondary colour).
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
