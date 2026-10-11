using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests;

/// <summary>Shape outlines and fills in the artistic brushes' textures (ticket 37).</summary>
public class TexturedShapeTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    public static TheoryData<ShapeStyle> TexturedStyles =>
        [ShapeStyle.Crayon, ShapeStyle.Marker, ShapeStyle.Oil, ShapeStyle.NaturalPencil, ShapeStyle.Watercolour];

    private static DocumentEditor CreateEditor(ITool tool, int width = 96, int height = 64) =>
        new(Document.CreateBlank(width, height)) { ActiveTool = tool, PrimaryColor = Red, SecondaryColor = Blue };

    private static ITool Brush(ShapeStyle style, int size)
    {
        var settings = new StrokeSettings(size);
        return style switch
        {
            ShapeStyle.Crayon => new ArtisticBrushTool(settings, BrushTip.Crayon),
            ShapeStyle.Marker => new ArtisticBrushTool(settings, BrushTip.Marker),
            ShapeStyle.NaturalPencil => new ArtisticBrushTool(settings, BrushTip.NaturalPencil),
            ShapeStyle.Oil => new OilBrushTool(settings),
            _ => new WatercolourBrushTool(settings),
        };
    }

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    // How much of a pure red or blue covers a pixel painted over white: 0 (none) to 1 (all).
    private static double Coverage(Rgba pixel) => 1 - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) / 255.0;

    private static List<double> CoverageIn(DocumentEditor editor, IntRect area)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        return [.. from y in Enumerable.Range(area.Y, area.Height)
                   from x in Enumerable.Range(area.X, area.Width)
                   select Coverage(pixels.GetPixel(x, y))];
    }

    [Fact]
    public void TheStyles_AreReferencePaintsOutlineAndFillMenus_InOrder()
    {
        Assert.Equal(
            [ShapeStyle.None, ShapeStyle.Solid, ShapeStyle.Crayon, ShapeStyle.Marker, ShapeStyle.Oil, ShapeStyle.NaturalPencil, ShapeStyle.Watercolour],
            Enum.GetValues<ShapeStyle>());
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void ATexturedLine_PaintsTheSamePixelsAsTheMatchingBrush(ShapeStyle style)
    {
        foreach (var size in ShapeSettings.Sizes)
        {
            var shape = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Line, Outline = style, Size = size }));
            Script.Stroke(shape, PointerButton.Left, (10.5, 12.5), (80.5, 47.5));
            shape.CommitPending();

            var brush = CreateEditor(Brush(style, size));
            Script.Stroke(brush, PointerButton.Left, (10.5, 12.5), (80.5, 47.5));

            Assert.Equal(Snapshot(brush), Snapshot(shape));
        }
    }

    /// <summary>
    /// A fill spreads the texture the brush leaves in the middle of a stroke: on average it covers
    /// about as much as the cores of brush strokes (their middle fifth) of the size an oil or
    /// watercolour fill spreads, and its grain is visible (not one flat tone) except with the marker.
    /// </summary>
    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void ATexturedFill_HasTheTextureOfTheMiddleOfABrushStroke(ShapeStyle style)
    {
        var shape = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = ShapeStyle.None, Fill = style }));
        Script.Stroke(shape, PointerButton.Left, (4.5, 4.5), (91.5, 59.5));
        shape.CommitPending();
        var fill = CoverageIn(shape, new IntRect(4, 4, 88, 56));

        var brush = CreateEditor(Brush(style, 20));
        var core = new List<double>();
        foreach (var y in new[] { 11, 32, 53 })
        {
            Script.Stroke(brush, PointerButton.Left, (4.5, y + 0.5), (91.5, y + 0.5));
            core.AddRange(CoverageIn(brush, new IntRect(14, y - 2, 68, 5)));
        }

        Assert.InRange(fill.Average(), core.Average() - 0.1, core.Average() + 0.1);
        Assert.Equal(style != ShapeStyle.Marker, fill.Max() - fill.Min() > 0.1);
        Assert.All(fill, c => Assert.InRange(c, 0, 1));
    }

    [Fact]
    public void ALargeFill_FilledInParallelRows_HasTheBrushTextureAtEveryPixel()
    {
        var shape = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = ShapeStyle.None, Fill = ShapeStyle.Crayon }), 420, 320);
        Script.Stroke(shape, PointerButton.Left, (5.5, 7.5), (414.5, 312.5));
        shape.CommitPending();

        var pixels = shape.Document.ActiveLayer.Pixels;
        for (var y = 7; y <= 312; y++)
        {
            for (var x = 5; x <= 414; x++)
            {
                Assert.Equal(Blue.WithOpacity(BrushTip.Crayon.Interior(x, y)).Over(Rgba.White), pixels.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void AMarkerFill_IsEvenTranslucentInk()
    {
        var shape = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Ellipse, Outline = ShapeStyle.None, Fill = ShapeStyle.Marker }));
        Script.Stroke(shape, PointerButton.Left, (4.5, 4.5), (91.5, 59.5));
        shape.CommitPending();

        Assert.Equal(Blue.WithOpacity(0.6f).Over(Rgba.White), shape.Document.ActiveLayer.Pixels.GetPixel(48, 32));
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void ATexturedOutline_StaysInsideTheFrame_AndPaintsAlongIt(ShapeStyle style)
    {
        foreach (var kind in new[] { ShapeKind.Rectangle, ShapeKind.Ellipse, ShapeKind.FivePointStar, ShapeKind.CloudCallout })
        {
            var editor = CreateEditor(new ShapeTool(new ShapeSettings { Kind = kind, Outline = style, Fill = ShapeStyle.None, Size = 8 }));
            Script.Stroke(editor, PointerButton.Left, (20.5, 10.5), (75.5, 53.5));
            var frame = editor.Adornment!.Value.Frame;
            editor.CommitPending();

            var pixels = editor.Document.ActiveLayer.Pixels;
            var painted = (from y in Enumerable.Range(0, pixels.Height)
                           from x in Enumerable.Range(0, pixels.Width)
                           where pixels.GetPixel(x, y) != Rgba.White
                           select (X: x, Y: y)).ToList();
            Assert.All(painted, p => Assert.True(
                p.X >= frame.X && p.Y >= frame.Y && p.X < frame.X + frame.Width && p.Y < frame.Y + frame.Height, $"{kind} {p}"));
            Assert.Contains(painted, p => p.Y < frame.Y + 8);                // along the top
            Assert.Contains(painted, p => p.Y >= frame.Y + frame.Height - 8); // and the bottom
            if (kind is ShapeKind.Rectangle or ShapeKind.Ellipse)
            {
                Assert.DoesNotContain((48, 32), painted); // not inside
            }
        }
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void ChangingTheStyle_OfAPendingShape_RedrawsItInTheNewStyle(ShapeStyle style)
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Ellipse, Outline = ShapeStyle.Solid, Fill = ShapeStyle.Solid, Size = 5 };
        var adjusted = CreateEditor(new ShapeTool(settings));
        Script.Stroke(adjusted, PointerButton.Left, (10.5, 6.5), (80.5, 56.5));
        var solid = adjusted.Compositor.Pixels.ToArray();

        (settings.Outline, settings.Fill) = (style, style);
        adjusted.RefreshPending();
        Assert.NotEqual(solid, adjusted.Compositor.Pixels.ToArray());
        Assert.False(adjusted.History.CanUndo); // still pending
        adjusted.CommitPending();

        var direct = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Ellipse, Outline = style, Fill = style, Size = 5 }));
        Script.Stroke(direct, PointerButton.Left, (10.5, 6.5), (80.5, 56.5));
        direct.CommitPending();
        Assert.Equal(Snapshot(direct), Snapshot(adjusted));

        (settings.Outline, settings.Fill) = (ShapeStyle.Solid, ShapeStyle.Solid);
        adjusted.Undo();
        Script.Stroke(adjusted, PointerButton.Left, (10.5, 6.5), (80.5, 56.5));
        Assert.Equal(solid, adjusted.Compositor.Pixels.ToArray()); // and back
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void APendingTexturedShape_KeepsItsTexture_WhileMovedAndResized(ShapeStyle style)
    {
        // Oil and watercolour are seeded from where the shape was started, so the texture doesn't change as it is adjusted.
        var adjusted = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = style, Fill = style, Size = 5 }));
        Script.Stroke(adjusted, PointerButton.Left, (10.5, 6.5), (40.5, 36.5));
        var before = adjusted.Compositor.Pixels.ToArray();
        Script.Stroke(adjusted, PointerButton.Left, (25.5, 20.5), (45.5, 30.5)); // move
        Script.Stroke(adjusted, PointerButton.Left, (61, 47), (81, 57));         // resize by the bottom-right handle
        Assert.NotEqual(before, adjusted.Compositor.Pixels.ToArray());
        Script.Stroke(adjusted, PointerButton.Left, (81, 57), (61, 47));         // and back
        Script.Stroke(adjusted, PointerButton.Left, (45.5, 30.5), (25.5, 20.5));
        Assert.Equal(before, adjusted.Compositor.Pixels.ToArray());
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void TheSameShape_AlwaysPaintsTheSamePixels(ShapeStyle style)
    {
        Rgba[] Draw()
        {
            var editor = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Heart, Outline = style, Fill = style, Size = 8 }));
            Script.Stroke(editor, PointerButton.Left, (6.5, 4.5), (88.5, 60.5));
            editor.Rotate(30);
            editor.CommitPending();
            return Snapshot(editor);
        }

        Assert.Equal(Draw(), Draw());
    }

    [Theory]
    [MemberData(nameof(TexturedStyles))]
    public void ATexturedShape_PartlyOffTheCanvas_PaintsOnlyWhatIsOnIt(ShapeStyle style)
    {
        var editor = CreateEditor(new ShapeTool(new ShapeSettings { Kind = ShapeKind.Ellipse, Outline = style, Fill = style, Size = 8 }));
        Script.Stroke(editor, PointerButton.Left, (-30.5, -20.5), (40.5, 30.5));
        Script.Stroke(editor, PointerButton.Left, (70.5, 40.5), (150.5, 90.5));
        editor.CommitPending();

        Assert.Contains(Snapshot(editor), p => p != Rgba.White);
        Assert.True(editor.History.CanUndo);
    }
}
