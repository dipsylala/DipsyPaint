using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class StrokeTests
{
    [Fact]
    public void FullOpacity_ReplacesThePixel()
    {
        var pixels = Document.CreateBlank(4, 4).ActiveLayer.Pixels;

        new Stroke(pixels, new Rgba(10, 20, 30, 255)).Plot(1, 1);

        Assert.Equal(new Rgba(10, 20, 30, 255), pixels.GetPixel(1, 1));
    }

    [Fact]
    public void PartialOpacity_BlendsOverTheLayerOncePerStroke()
    {
        var pixels = Document.CreateBlank(8, 1).ActiveLayer.Pixels;
        var stroke = new Stroke(pixels, Rgba.Black, opacity: 0.5f);

        stroke.Line(0, 0, 7, 0);
        stroke.Line(7, 0, 0, 0);

        Assert.All(Enumerable.Range(0, 8), x => Assert.Equal(new Rgba(127, 127, 127, 255), pixels.GetPixel(x, 0)));
    }

    [Fact]
    public void TakeDirty_ReturnsChangedPixelsSinceLastCall()
    {
        var pixels = Document.CreateBlank(8, 8).ActiveLayer.Pixels;
        var stroke = new Stroke(pixels, Rgba.Black);

        stroke.Line(1, 1, 3, 2);
        var first = stroke.TakeDirty();
        stroke.Plot(-1, 0);
        var second = stroke.TakeDirty();

        Assert.Equal(new IntRect(1, 1, 3, 2), first);
        Assert.True(second.IsEmpty);
    }
}
