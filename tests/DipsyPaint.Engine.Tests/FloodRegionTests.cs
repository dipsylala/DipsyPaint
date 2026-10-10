namespace DipsyPaint.Engine.Tests;

public class FloodRegionTests
{
    private static IPixelAccess Blank(int width, int height) => Document.CreateBlank(width, height).ActiveLayer.Pixels;

    private static HashSet<(int X, int Y)> PixelsOf(List<IntRect> spans)
    {
        var pixels = new HashSet<(int, int)>();
        foreach (var span in spans)
        {
            Assert.Equal(1, span.Height);
            for (var x = span.X; x < span.Right; x++)
            {
                Assert.True(pixels.Add((x, span.Y)), "spans must not overlap");
            }
        }

        return pixels;
    }

    [Fact]
    public void BlankLayer_IsOneSpanPerRow()
    {
        var spans = FloodRegion.Find(Blank(7, 5), 3, 2);

        Assert.Equal(5, spans.Count);
        Assert.All(spans, s => Assert.Equal((0, 7), (s.X, s.Width)));
    }

    [Fact]
    public void Region_GoesAroundHolesAndExcludesThem()
    {
        var pixels = Blank(5, 5);
        pixels.SetPixel(2, 2, Rgba.Black); // a hole in the middle
        pixels.SetPixel(0, 4, Rgba.Black); // and one on the edge

        var region = PixelsOf(FloodRegion.Find(pixels, 0, 0));

        Assert.Equal(25 - 2, region.Count);
        Assert.DoesNotContain((2, 2), region);
        Assert.DoesNotContain((0, 4), region);
    }

    [Fact]
    public void Region_DoesNotStepDiagonally()
    {
        var pixels = Blank(3, 3);
        pixels.SetPixel(1, 0, Rgba.Black);
        pixels.SetPixel(0, 1, Rgba.Black);

        Assert.Equal([(0, 0)], PixelsOf(FloodRegion.Find(pixels, 0, 0)));
    }

    [Fact]
    public void Tolerance_IncludesPixelsWithinItOfTheSeedOnEveryChannel()
    {
        var pixels = Blank(4, 1);
        pixels.SetPixel(1, 0, new Rgba(250, 250, 250, 255));
        pixels.SetPixel(2, 0, new Rgba(255, 255, 249, 255));

        Assert.Single(PixelsOf(FloodRegion.Find(pixels, 0, 0)));
        Assert.Equal(2, PixelsOf(FloodRegion.Find(pixels, 0, 0, tolerance: 5)).Count);
        Assert.Equal(4, PixelsOf(FloodRegion.Find(pixels, 0, 0, tolerance: 6)).Count);
    }

    [Fact]
    public void SeedOffTheLayer_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FloodRegion.Find(Blank(4, 4), 4, 0));
    }
}
