using System.Buffers.Binary;
using DipsyPaint.Engine.Codecs;
using static DipsyPaint.Engine.Tests.Codecs.SampleFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

public class ClipboardCodecTests
{
    /// <summary>A 3×2 image with opaque, half-transparent and fully transparent pixels.</summary>
    private static PixelImage Sample() => new(3, 2,
    [
        new Rgba(255, 0, 0, 255), new Rgba(0, 128, 0, 128), Rgba.Transparent,
        new Rgba(10, 20, 30, 255), new Rgba(50, 0, 50, 64), new Rgba(0, 0, 255, 255),
    ]);

    /// <summary>A Windows clipboard bitmap (CF_DIB): a BMP file without its 14-byte file header.</summary>
    private static byte[] Dib(Document document) => Export(document, ImageFormat.Bmp)[14..];

    private static byte[] Garbage(int length)
    {
        var bytes = new byte[length];
        new Random(1234).NextBytes(bytes);
        return bytes;
    }

    [Fact]
    public void Png_RoundTripsExactly_IncludingAlpha()
    {
        var image = Sample();

        var decoded = ClipboardCodec.Decode(ClipboardCodec.EncodePng(image), ClipboardEncoding.Png);

        Assert.Equal((3, 2), (decoded.Width, decoded.Height));
        Assert.Equal(image.Pixels, decoded.Pixels);
    }

    [Fact]
    public void Dib_Decodes()
    {
        var document = Gradient(5, 3);

        var decoded = ClipboardCodec.Decode(Dib(document), ClipboardEncoding.Dib);

        Assert.Equal((5, 3), (decoded.Width, decoded.Height));
        Assert.Equal(document.ActiveLayer.Pixels.GetPixel(4, 2), decoded.Pixels[2 * 5 + 4]);
    }

    [Fact]
    public void Dib_With32BitPixelsAndNoAlpha_IsOpaque()
    {
        // Windows apps often put 32-bit BI_RGB bitmaps on the clipboard with the fourth byte zero.
        var dib = new byte[40 + 2 * 4];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), 2);  // width
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), 1);  // height
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(12), 1); // planes
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(14), 32);
        dib[40] = 255;                                              // blue, alpha 0
        dib[46] = 255;                                              // red, alpha 0

        var decoded = ClipboardCodec.Decode(dib, ClipboardEncoding.Dib);

        Assert.Equal([new Rgba(0, 0, 255, 255), new Rgba(255, 0, 0, 255)], decoded.Pixels);
    }

    [Theory]
    [InlineData(ClipboardEncoding.Png)]
    [InlineData(ClipboardEncoding.Dib)]
    [InlineData(ClipboardEncoding.Tiff)]
    public void Garbage_FailsAsUnreadable(ClipboardEncoding encoding)
    {
        var failure = Assert.Throws<ImageOpenException>(() => ClipboardCodec.Decode(Garbage(256), encoding));

        Assert.Equal(OpenFailure.Unreadable, failure.Failure);
    }

    [Fact]
    public void OnlyTheLabelledEncodingIsParsed()
    {
        var png = ClipboardCodec.EncodePng(Sample());

        Assert.Throws<ImageOpenException>(() => ClipboardCodec.Decode(png, ClipboardEncoding.Tiff));
    }

    [Fact]
    public void TruncatedDibHeader_FailsAsUnreadable()
    {
        Assert.Throws<ImageOpenException>(() => ClipboardCodec.Decode(new byte[12], ClipboardEncoding.Dib));
    }

    [Fact]
    public void ImageBeyondTheSizeLimit_IsRefusedBeforeDecoding()
    {
        var bomb = Png(Document.MaxSide + 1, 1, Zlib([0]));

        var failure = Assert.Throws<ImageOpenException>(() => ClipboardCodec.Decode(bomb, ClipboardEncoding.Png));

        Assert.Equal(OpenFailure.TooLarge, failure.Failure);
    }

    [Fact]
    public void ImageBeyondTheMemoryBudget_IsRefused()
    {
        var png = ClipboardCodec.EncodePng(Sample());

        var failure = Assert.Throws<ImageOpenException>(() =>
            ClipboardCodec.Decode(png, ClipboardEncoding.Png, new MemoryBudget(6 * ClipboardCodec.PasteCostPerPixel - 1)));

        Assert.Equal(OpenFailure.OverMemoryBudget, failure.Failure);
    }
}
