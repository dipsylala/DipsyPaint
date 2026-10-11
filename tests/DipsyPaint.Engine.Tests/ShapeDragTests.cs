using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests;

/// <summary>
/// Handle drags of a large pending shape (issue #102). While a handle is dragged, the shape shown is
/// the exact shape for the pointer's position, not a cheaper preview: it covers the same pixels, with
/// the same texture, as the shape drawn or turned there directly, and releasing the handle changes
/// nothing. The shapes are large enough (over 2^16 pixels) to take the parallel paths.
/// </summary>
public class ShapeDragTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    public static TheoryData<ShapeStyle, ShapeStyle> Styles =>
        new()
        {
            { ShapeStyle.Solid, ShapeStyle.Solid },
            { ShapeStyle.Crayon, ShapeStyle.Crayon },
            { ShapeStyle.Marker, ShapeStyle.Marker },
            { ShapeStyle.Oil, ShapeStyle.Oil },
            { ShapeStyle.NaturalPencil, ShapeStyle.NaturalPencil },
            { ShapeStyle.Watercolour, ShapeStyle.Watercolour },
            { ShapeStyle.Solid, ShapeStyle.Oil },
            { ShapeStyle.Crayon, ShapeStyle.None },
        };

    private static DocumentEditor CreateEditor(ShapeKind kind, ShapeStyle outline, ShapeStyle fill)
    {
        var document = Document.CreateBlank(Width, Height);
        document.AddLayer();
        var settings = new ShapeSettings { Kind = kind, Outline = outline, Fill = fill, Size = 8 };
        return new DocumentEditor(document) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red, SecondaryColor = Blue };
    }

    private static DocumentEditor Drawn(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, (double X, double Y) to)
    {
        var editor = CreateEditor(kind, outline, fill);
        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), to);
        return editor;
    }

    private static Rgba[] Composite(DocumentEditor editor) => editor.Compositor.Pixels.ToArray();

    [Theory]
    [MemberData(nameof(Styles))]
    public void AStretchDrag_ShowsTheShapeDrawnToThatFrame_AndReleasingChangesNothing(ShapeStyle outline, ShapeStyle fill)
    {
        var editor = Drawn(ShapeKind.Ellipse, outline, fill, (300.5, 200.5));
        var frame = editor.Adornment!.Value.Frame;
        editor.Apply(new PointerInput(PointerAction.Down, frame.Right, frame.Bottom));
        editor.Apply(new PointerInput(PointerAction.Move, frame.Right + 40, frame.Bottom + 30));
        editor.Apply(new PointerInput(PointerAction.Move, frame.Right + 60, frame.Bottom + 50));
        var during = Composite(editor);

        var expected = Drawn(ShapeKind.Ellipse, outline, fill, (360.5, 250.5));
        Assert.Equal(expected.Adornment, editor.Adornment);
        Assert.Equal(Composite(expected), during);

        editor.Apply(new PointerInput(PointerAction.Up, frame.Right + 60, frame.Bottom + 50));
        Assert.Equal(during, Composite(editor));
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void ARotateDrag_ShowsTheShapeTurnedThere_AndReleasingChangesNothing(ShapeStyle outline, ShapeStyle fill)
    {
        var editor = Drawn(ShapeKind.Heart, outline, fill, (330.5, 260.5));
        var frame = editor.Adornment!.Value.Frame;
        double centreX = frame.X + frame.Width / 2.0, centreY = frame.Y + frame.Height / 2.0;
        var (x, y) = editor.Adornment!.Value.RotateHandle(editor.HandleTolerance);
        var radius = centreY - y;
        // Shift snaps the turn to exactly the angle asked for.
        PointerInput Turn(PointerAction action, double degrees) => new(
            action, centreX + radius * Math.Sin(degrees * Math.PI / 180), centreY - radius * Math.Cos(degrees * Math.PI / 180), Shift: true);

        editor.Apply(new PointerInput(PointerAction.Down, x, y));
        editor.Apply(Turn(PointerAction.Move, 15));
        editor.Apply(Turn(PointerAction.Move, 45));
        editor.Apply(Turn(PointerAction.Move, 30));
        var during = Composite(editor);

        var expected = Drawn(ShapeKind.Heart, outline, fill, (330.5, 260.5));
        expected.Rotate(30);
        Assert.Equal(expected.Adornment, editor.Adornment);
        Assert.Equal(Composite(expected), during);

        editor.Apply(Turn(PointerAction.Up, 30));
        Assert.Equal(during, Composite(editor));
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void AMoveDrag_ShowsTheShapeWhereItIsDropped_WhicheverWayTheDragWent(ShapeStyle outline, ShapeStyle fill)
    {
        var editor = Drawn(ShapeKind.Rectangle, outline, fill, (300.5, 200.5));
        var direct = Drawn(ShapeKind.Rectangle, outline, fill, (300.5, 200.5));
        Script.Stroke(direct, PointerButton.Left, (150.5, 100.5), (190.5, 70.5));

        editor.Apply(new PointerInput(PointerAction.Down, 150.5, 100.5));
        editor.Apply(new PointerInput(PointerAction.Move, 120.5, 150.5));
        editor.Apply(new PointerInput(PointerAction.Move, 170.5, 90.5));
        editor.Apply(new PointerInput(PointerAction.Move, 190.5, 70.5));
        var during = Composite(editor);

        Assert.Equal(new IntRect(60, -10, 281, 181), editor.Adornment!.Value.Frame);
        Assert.Equal(direct.Adornment, editor.Adornment);
        Assert.Equal(Composite(direct), during);
        editor.Apply(new PointerInput(PointerAction.Up, 190.5, 70.5));
        Assert.Equal(during, Composite(editor));
    }

    /// <summary>
    /// An oil fill's texture lies in the shape's frame: after the shape is moved and stretched, every
    /// pixel of the fill has the texture at its place in the frame, computed directly.
    /// </summary>
    [Fact]
    public void AnOilFill_KeepsItsTextureInTheFrame_WhileDragged()
    {
        var editor = Drawn(ShapeKind.Rectangle, ShapeStyle.None, ShapeStyle.Oil, (300.5, 200.5));
        Script.Stroke(editor, PointerButton.Left, (150.5, 100.5), (230.5, 140.5)); // move
        var frame = editor.Adornment!.Value.Frame;
        Script.Stroke(editor, PointerButton.Left, (frame.Right, frame.Bottom), (frame.Right + 50, frame.Bottom + 10));
        frame = editor.Adornment!.Value.Frame;
        Assert.Equal(new IntRect(100, 60, 331, 191), frame);

        var tip = new OilTip(10, ArtisticNoise.Hash(ArtisticNoise.Seed(20, 20)));
        var composite = editor.Compositor.Pixels;
        for (var y = frame.Y; y < frame.Bottom; y++)
        {
            for (var x = frame.X; x < Width; x++)
            {
                var coverage = Math.Clamp((float)tip.Texture(y + 0.5 - frame.Y, x + 0.5 - frame.X, double.PositiveInfinity), 0f, 1f);
                Assert.Equal(Blue.WithOpacity(coverage).Over(Rgba.White), composite[y * Width + x]);
            }
        }
    }

    /// <summary>
    /// A long textured line is swept in parallel bands of rows; it leaves exactly what the matching
    /// brush, which sweeps the same path in one pass, leaves.
    /// </summary>
    [Theory]
    [InlineData(ShapeStyle.Crayon)]
    [InlineData(ShapeStyle.Marker)]
    [InlineData(ShapeStyle.Oil)]
    [InlineData(ShapeStyle.NaturalPencil)]
    [InlineData(ShapeStyle.Watercolour)]
    public void ALongTexturedLine_PaintsTheSamePixelsAsTheMatchingBrush(ShapeStyle style)
    {
        var shape = CreateEditor(ShapeKind.Line, style, ShapeStyle.None);
        Script.Stroke(shape, PointerButton.Left, (10.5, 290.5), (390.5, 6.5));
        shape.CommitPending();

        var document = Document.CreateBlank(Width, Height);
        document.AddLayer();
        var settings = new StrokeSettings(8);
        ITool brush = style switch
        {
            ShapeStyle.Crayon => new ArtisticBrushTool(settings, BrushTip.Crayon),
            ShapeStyle.Marker => new ArtisticBrushTool(settings, BrushTip.Marker),
            ShapeStyle.NaturalPencil => new ArtisticBrushTool(settings, BrushTip.NaturalPencil),
            ShapeStyle.Oil => new OilBrushTool(settings),
            _ => new WatercolourBrushTool(settings),
        };
        var brushed = new DocumentEditor(document) { ActiveTool = brush, PrimaryColor = Red, SecondaryColor = Blue };
        Script.Stroke(brushed, PointerButton.Left, (10.5, 290.5), (390.5, 6.5));

        Assert.Equal(Composite(brushed), Composite(shape));
    }
}
