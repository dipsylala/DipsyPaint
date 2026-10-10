namespace DipsyPaint.Engine.Tests;

public class PixelAccessTests
{
    private static IPixelAccess Pixels(int width = 4, int height = 3) => Document.CreateBlank(width, height).ActiveLayer.Pixels;

    [Fact]
    public void WriteRegionThenReadRegion_RoundTrips()
    {
        var pixels = Pixels();
        Rgba[] block = [Rgba.Black, new(1, 2, 3, 255), new(4, 5, 6, 255), Rgba.Transparent];

        pixels.WriteRegion(new IntRect(1, 1, 2, 2), block);
        var read = new Rgba[4];
        pixels.ReadRegion(new IntRect(1, 1, 2, 2), read);

        Assert.Equal(block, read);
        Assert.Equal(new Rgba(1, 2, 3, 255), pixels.GetPixel(2, 1));
        Assert.Equal(Rgba.White, pixels.GetPixel(0, 0));
    }

    [Fact]
    public void SetPixelThenGetPixel_RoundTrips()
    {
        var pixels = Pixels();

        pixels.SetPixel(3, 2, Rgba.Black);

        Assert.Equal(Rgba.Black, pixels.GetPixel(3, 2));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void OutOfBoundsPixel_Throws(int x, int y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pixels().GetPixel(x, y));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pixels().SetPixel(x, y, Rgba.Black));
    }

    [Fact]
    public void RegionOutsideLayerOrBufferTooSmall_Throws()
    {
        var pixels = Pixels();

        Assert.Throws<ArgumentOutOfRangeException>(() => pixels.ReadRegion(new IntRect(3, 0, 2, 1), new Rgba[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => pixels.ReadRegion(new IntRect(0, 0, 2, 2), new Rgba[3]));
    }
}
