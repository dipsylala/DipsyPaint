using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Freeform rotate and skew (ticket 23) at arbitrary angles, of selections, shapes, text and the
/// whole picture. Pixels are pure engine maths (bilinear resampling, shapes turned before they are
/// rasterised), so every OS gives the same result.
/// </summary>
public class FreeTransformGoldenTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    /// <summary>A white document with coloured 8×8 tiles, as in <see cref="SelectionGoldenTests"/>.</summary>
    private static DocumentEditor CreateTiled(int width, int height, ITool? tool = null)
    {
        var editor = new DocumentEditor(Document.CreateBlank(width, height)) { ActiveTool = tool };
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var (tx, ty) = (x / 8, y / 8);
                layer.SetPixel(x, y, (tx + ty) % 2 == 0 ? Rgba.White : new Rgba((byte)(tx * 30), (byte)(ty * 40), 160, 255));
            }
        }

        editor.Compositor.Update(new IntRect(0, 0, layer.Width, layer.Height));
        return editor;
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(137)]
    [InlineData(-20)]
    public void RotateSelection(int degrees)
    {
        var editor = CreateTiled(64, 64, new SelectionTool());
        editor.Select(new IntRect(16, 22, 32, 20));

        editor.Rotate(degrees);
        editor.CommitPending();

        GoldenImage.AssertMatches($"rotate-selection-{(degrees < 0 ? "minus" : "")}{Math.Abs(degrees)}", editor.Compositor);
    }

    [Fact]
    public void RotateFreeFormSelection_WithTheHandle()
    {
        var editor = CreateTiled(64, 64, new SelectionTool(new SelectionSettings { Shape = SelectionShape.FreeForm }));
        editor.HandleTolerance = 1;
        // A triangle, 24 wide and 16 tall.
        Script.Stroke(editor, PointerButton.Left, (20.5, 40.5), (32.5, 24.5), (44.5, 40.5), (20.5, 40.5));
        var frame = editor.Adornment!.Value.Frame;
        var (cx, cy) = (frame.X + frame.Width / 2.0, frame.Y + frame.Height / 2.0);
        var (hx, hy) = editor.Adornment!.Value.RotateHandle(1);

        // Drag the handle 60° clockwise about the centre.
        var r = cy - hy;
        Script.Stroke(editor, PointerButton.Left, (hx, hy), (cx + r * Math.Sin(Math.PI / 3), cy - r * Math.Cos(Math.PI / 3)));
        Script.Stroke(editor, PointerButton.Left, (cx, cy), (cx + 6, cy - 4)); // and move it
        editor.CommitPending();

        GoldenImage.AssertMatches("rotate-selection-freeform-handle", editor.Compositor);
    }

    [Theory]
    [InlineData(20, 0, "skew-selection-h20")]
    [InlineData(0, -15, "skew-selection-vminus15")]
    [InlineData(20, -15, "skew-selection-h20-vminus15")]
    public void SkewSelection(int horizontal, int vertical, string golden)
    {
        var editor = CreateTiled(64, 64, new SelectionTool());
        editor.Select(new IntRect(8, 16, 32, 24));

        editor.ResizeAndSkew(32, 24, horizontal, vertical);
        editor.CommitPending();

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }

    [Theory]
    [InlineData(30, 0, 0, "rotate-picture-30")]
    [InlineData(0, 20, 0, "skew-picture-h20")]
    [InlineData(0, 0, -15, "skew-picture-vminus15")]
    public void TransformPicture(int degrees, int horizontal, int vertical, string golden)
    {
        var editor = CreateTiled(40, 24);

        if (degrees != 0)
        {
            editor.Rotate(degrees);
        }
        else
        {
            editor.ResizeAndSkew(40, 24, horizontal, vertical);
        }

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle, 30)]
    [InlineData(ShapeKind.Ellipse, 45)]
    [InlineData(ShapeKind.FivePointStar, 137)]
    [InlineData(ShapeKind.RightArrow, -60)]
    public void RotateShape(ShapeKind kind, int degrees)
    {
        var settings = new ShapeSettings { Kind = kind, Outline = ShapeStyle.Solid, Fill = ShapeStyle.Solid, Size = 3 };
        var editor = new DocumentEditor(Document.CreateBlank(64, 64))
        {
            ActiveTool = new ShapeTool(settings),
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };
        Script.Stroke(editor, PointerButton.Left, (12.5, 20.5), (51.5, 43.5)); // 40×24

        editor.Rotate(degrees);
        editor.CommitPending();

        GoldenImage.AssertMatches($"rotate-shape-{kind.ToString().ToLowerInvariant()}-{(degrees < 0 ? "minus" : "")}{Math.Abs(degrees)}", editor.Compositor);
    }

    [Fact]
    public void RotateLineAndPolygon()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Line, Outline = ShapeStyle.Solid, Size = 3 };
        var editor = new DocumentEditor(Document.CreateBlank(64, 64)) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red, SecondaryColor = Blue };
        Script.Stroke(editor, PointerButton.Left, (8.5, 12.5), (40.5, 12.5));
        editor.Rotate(30);
        editor.CommitPending();

        settings.Kind = ShapeKind.Polygon;
        settings.Size = 1;
        Script.Stroke(editor, PointerButton.Left, (20.5, 36.5), (50.5, 36.5));
        Script.Stroke(editor, PointerButton.Left, (50.5, 56.5));
        Script.Stroke(editor, PointerButton.Left, (30.5, 50.5));
        Script.Stroke(editor, PointerButton.Left, (20.5, 36.5)); // closes
        editor.Rotate(-45);
        editor.CommitPending();

        GoldenImage.AssertMatches("rotate-line-polygon", editor.Compositor);
    }

    [Fact]
    public void RotateText()
    {
        var tool = new TextTool(new TextSettings(), new BarsRenderer());
        var editor = new DocumentEditor(Document.CreateBlank(64, 64)) { ActiveTool = tool, PrimaryColor = Red, SecondaryColor = Blue };
        Script.Stroke(editor, PointerButton.Left, (12.5, 24.5), (51.5, 39.5)); // a 40×16 box
        tool.Text = "text";
        editor.RefreshPending();

        editor.Rotate(30);
        editor.CommitPending();

        GoldenImage.AssertMatches("rotate-text-30", editor.Compositor);
    }

    /// <summary>Stands in for a font: alternate 2-pixel columns of the box are filled for the first half of its height.</summary>
    private sealed class BarsRenderer : ITextRenderer
    {
        public PixelImage Render(TextRequest request)
        {
            var (width, height) = (request.Width, request.MinHeight);
            var pixels = new Rgba[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = (x / 2) % 2 == 0 && y < height / 2 ? request.Foreground : request.Background ?? Rgba.Transparent;
                }
            }

            return new PixelImage(width, height, pixels);
        }
    }
}
