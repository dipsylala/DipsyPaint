using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Crop, resize, rotate and flip on a 48×32 picture of 8×8 tiles whose colour depends on their
/// position, with a black arrow in the top-left tile pointing right and a transparent tile at the
/// bottom right, so orientation, resampling and transparency all show in the golden images.
/// </summary>
public class ImageGoldenTests
{
    private static DocumentEditor CreateEditor()
    {
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { ActiveTool = new SelectionTool(), HandleTolerance = 0.25 };
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var (tx, ty) = (x / 8, y / 8);
                var colour = (tx, ty) == (5, 3) ? Rgba.Transparent
                    : (tx + ty) % 2 == 0 ? Rgba.White
                    : new Rgba((byte)(tx * 50), (byte)(ty * 80), 160, 255);
                layer.SetPixel(x, y, colour);
            }
        }

        // An arrow pointing right: a shaft along row 3 and a head at its right end.
        for (var x = 1; x < 7; x++)
        {
            layer.SetPixel(x, 3, Rgba.Black);
        }

        layer.SetPixel(5, 2, Rgba.Black);
        layer.SetPixel(5, 4, Rgba.Black);
        layer.SetPixel(4, 1, Rgba.Black);
        layer.SetPixel(4, 5, Rgba.Black);

        editor.Compositor.Update(new IntRect(0, 0, layer.Width, layer.Height));
        return editor;
    }

    [Theory]
    [InlineData(ImageTransform.RotateRight, "image-rotate-right")]
    [InlineData(ImageTransform.RotateLeft, "image-rotate-left")]
    [InlineData(ImageTransform.Rotate180, "image-rotate-180")]
    [InlineData(ImageTransform.FlipVertical, "image-flip-vertical")]
    [InlineData(ImageTransform.FlipHorizontal, "image-flip-horizontal")]
    public void Transform(ImageTransform transform, string golden)
    {
        var editor = CreateEditor();

        editor.Transform(transform);

        GoldenImage.AssertMatches(golden, editor.Compositor);
    }

    [Fact]
    public void Crop()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (19.5, 11.5)); // 20×12 from the top left
        editor.CropToSelection();

        GoldenImage.AssertMatches("image-crop", editor.Compositor);
    }

    [Fact]
    public void ResizeLarger()
    {
        var editor = CreateEditor();

        editor.Resize(72, 48); // 150%

        GoldenImage.AssertMatches("image-resize-larger", editor.Compositor);
    }

    [Fact]
    public void ResizeSmaller()
    {
        var editor = CreateEditor();

        editor.Resize(20, 20); // 41.7% × 62.5%, aspect ratio not kept

        GoldenImage.AssertMatches("image-resize-smaller", editor.Compositor);
    }

    [Fact]
    public void RotateAndFlipASelection()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (15.5, 7.5)); // the top-left two tiles
        editor.Transform(ImageTransform.RotateRight);
        editor.Transform(ImageTransform.FlipHorizontal);
        editor.CommitPending();

        GoldenImage.AssertMatches("image-selection-rotate-flip", editor.Compositor);
    }
}
