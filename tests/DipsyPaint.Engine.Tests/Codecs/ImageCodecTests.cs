using DipsyPaint.Engine.Codecs;
using static DipsyPaint.Engine.Tests.Codecs.SampleFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

public class ImageCodecTests
{
    private static Rgba[] Composite(Document document) => new Compositor(document).Pixels.ToArray();

    private static int LargestChannelDifference(Rgba[] a, Rgba[] b) => a.Zip(b).Max(p => Math.Max(
        Math.Max(Math.Abs(p.First.R - p.Second.R), Math.Abs(p.First.G - p.Second.G)),
        Math.Max(Math.Abs(p.First.B - p.Second.B), Math.Abs(p.First.A - p.Second.A))));

    [Fact]
    public void Png_RoundTripsEveryPixelExactly_IncludingTranslucency()
    {
        var document = Document.CreateBlank(64, 48);
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var a = (byte)((x * 37 + y * 11) % 256);
                document.ActiveLayer.Pixels.SetPixel(x, y, new Rgba((byte)(x * 7 % (a + 1)), (byte)(y * 5 % (a + 1)), (byte)(a / 2), a));
            }
        }

        var reopened = Open(Export(document, ImageFormat.Png));

        Assert.Equal((64, 48), (reopened.Width, reopened.Height));
        Assert.Equal(Composite(document), Composite(reopened));
    }

    [Fact]
    public void Bmp_RoundTripsOpaquePixelsExactly_As24Bit()
    {
        var document = Gradient();

        var file = Export(document, ImageFormat.Bmp);

        Assert.Equal("BM"u8.ToArray(), file[..2]);
        Assert.Equal(24, BitConverter.ToInt16(file, 28));
        Assert.Equal(Composite(document), Composite(Open(file)));
    }

    [Fact]
    public void Jpeg_RoundTripsWithinCompressionLoss()
    {
        var document = Gradient(64, 48);

        var reopened = Open(Export(document, ImageFormat.Jpeg));

        Assert.Equal((64, 48), (reopened.Width, reopened.Height));
        Assert.InRange(LargestChannelDifference(Composite(document), Composite(reopened)), 0, 12);
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Bmp)]
    public void FormatsWithoutAlpha_FlattenOntoWhite(ImageFormat format)
    {
        var document = Document.CreateBlank(16, 16);
        var transparent = new Rgba[16 * 16];
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(0, 0, 16, 16), transparent);

        var reopened = Open(Export(document, format));

        Assert.All(Composite(reopened), p => Assert.True(p.R >= 250 && p.G >= 250 && p.B >= 250 && p.A == 255, p.ToString()));
    }

    [Fact]
    public void Export_WritesTheCompositeAndKeepsNoLayers()
    {
        var document = Document.CreateBlank(8, 8);
        document.ActiveLayer.Pixels.SetPixel(1, 1, new Rgba(255, 0, 0, 255));
        var top = document.AddLayer();
        top.Pixels.SetPixel(1, 1, new Rgba(0, 0, 128, 128)); // half-transparent blue over red
        top.Pixels.SetPixel(5, 5, Rgba.Black);

        var reopened = Open(Export(document, ImageFormat.Png));

        Assert.Single(reopened.Layers);
        Assert.Equal(Composite(document), Composite(reopened));
        Assert.Equal(new Rgba(127, 0, 128, 255), reopened.ActiveLayer.Pixels.GetPixel(1, 1));
    }

    [Fact]
    public void Open_ReadsDibAndAllJpegExtensionsByContent()
    {
        var bmp = Export(Gradient(), ImageFormat.Bmp);

        Assert.Equal(32, Open(bmp).Width);
        Assert.Equal(ImageFormat.Bmp, ImageCodec.FormatOf("picture.DIB"));
        Assert.All(new[] { "a.jpg", "a.jpeg", "a.jpe", "a.JFIF" }, name => Assert.Equal(ImageFormat.Jpeg, ImageCodec.FormatOf(name)));
        Assert.Equal(ImageFormat.Png, ImageCodec.FormatOf("a.png"));
        Assert.Null(ImageCodec.FormatOf("a.gif"));
        Assert.Null(ImageCodec.FormatOf("noextension"));
    }

    [Fact]
    public void Open_AcceptsTheMaximumSideWithinBudget()
    {
        var file = Export(Document.CreateBlank(Document.MaxSide, 1), ImageFormat.Png);

        Assert.Equal(Document.MaxSide, Open(file).Width);
    }

    [Theory]
    [InlineData(Document.MaxSide + 1, 1)]
    [InlineData(1, Document.MaxSide + 1)]
    public void Open_RejectsImagesOverTheSideLimit(int width, int height)
    {
        var file = Png(width, height, Zlib(new byte[16]));

        var error = Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.Equal(OpenFailure.TooLarge, error.Failure);
    }

    [Fact]
    public void Open_RejectsImagesOverTheMemoryBudget()
    {
        var file = Export(Gradient(100, 100), ImageFormat.Png);
        var cost = 100 * 100 * ImageCodec.OpenCostPerPixel;

        var error = Assert.Throws<ImageOpenException>(() => Open(file, new MemoryBudget(cost - 1)));

        Assert.Equal(OpenFailure.OverMemoryBudget, error.Failure);
        Assert.Equal(100, Open(file, new MemoryBudget(cost)).Width);
    }

    [Fact]
    public void DefaultMemoryBudget_IsThePlaceholderValue()
    {
        Assert.Equal(2L * 1024 * 1024 * 1024, MemoryBudget.Default.Bytes);
    }

    /// <summary>
    /// These headers are followed by no usable pixel data, so decoding would fail as unreadable.
    /// Getting the size failure instead shows the header was checked before decoding.
    /// </summary>
    [Theory]
    [InlineData("png-side", OpenFailure.TooLarge)]
    [InlineData("bmp-side", OpenFailure.TooLarge)]
    [InlineData("jpeg-side", OpenFailure.TooLarge)]
    [InlineData("png-budget", OpenFailure.OverMemoryBudget)]
    public void Open_ChecksHeaderSizeBeforeDecoding(string sample, OpenFailure expected)
    {
        var file = sample switch
        {
            "png-side" => Png(20000, 20000, [1, 2, 3]),
            "bmp-side" => BmpHeader(100000, 10),
            "jpeg-side" => JpegDeclaring(60000, 60000),
            _ => Png(16000, 16000, [1, 2, 3]), // within the side limit, ~4 GB to open
        };

        var error = Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.True(expected == error.Failure, $"{error.Failure}: {error.InnerException}");
    }

    [Fact]
    public void Open_ReadsFromNonSeekableStreams()
    {
        var file = Export(Gradient(), ImageFormat.Png);

        var document = ImageCodec.Open(new NonSeekableStream(file));

        Assert.Equal(32, document.Width);
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
