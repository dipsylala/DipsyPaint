using System.Buffers.Binary;
using DipsyPaint.Engine.Codecs;
using SixLabors.ImageSharp.PixelFormats;
using static DipsyPaint.Engine.Tests.Codecs.FormatSamples;
using static DipsyPaint.Engine.Tests.Codecs.SampleFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>GIF, TIFF, ICO, WebP and the low-colour BMPs (ticket 25, ADR-0019).</summary>
public class RemainingFormatsTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    // The standard Windows 16 colours, which 16-colour bitmaps use.
    private static readonly HashSet<Rgba> SixteenColours =
    [
        new(0, 0, 0, 255), new(128, 0, 0, 255), new(0, 128, 0, 255), new(128, 128, 0, 255),
        new(0, 0, 128, 255), new(128, 0, 128, 255), new(0, 128, 128, 255), new(192, 192, 192, 255),
        new(128, 128, 128, 255), new(255, 0, 0, 255), new(0, 255, 0, 255), new(255, 255, 0, 255),
        new(0, 0, 255, 255), new(255, 0, 255, 255), new(0, 255, 255, 255), new(255, 255, 255, 255),
    ];

    private static Rgba[] Composite(Document document) => new Compositor(document).Pixels.ToArray();

    /// <summary>An opaque image using exactly <paramref name="colours"/> distinct colours, in stripes.</summary>
    private static Document Stripes(int colours, int width = 64, int height = 32)
    {
        var document = Document.CreateBlank(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (x + y * width) % colours;
                document.ActiveLayer.Pixels.SetPixel(x, y, new Rgba((byte)(i * 37), (byte)(i * 11), (byte)(255 - i), 255));
            }
        }

        return document;
    }

    [Fact]
    public void FormatTable_MatchesAdr0019()
    {
        Assert.Equal(
            [ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Bmp, ImageFormat.Gif, ImageFormat.Tiff,
             ImageFormat.Bmp256, ImageFormat.Bmp16, ImageFormat.BmpMonochrome, ImageFormat.OpenRaster],
            Enum.GetValues<ImageFormat>().Where(ImageCodec.CanExport));
        Assert.False(ImageCodec.CanExport(ImageFormat.Ico));
        Assert.False(ImageCodec.CanExport(ImageFormat.Webp));
        Assert.Equal(
            [ImageFormat.Gif, ImageFormat.Bmp256, ImageFormat.Bmp16, ImageFormat.BmpMonochrome],
            Enum.GetValues<ImageFormat>().Where(ImageCodec.UsesPalette));

        Assert.Equal(ImageFormat.Gif, ImageCodec.FormatOf("a.GIF"));
        Assert.Equal(ImageFormat.Tiff, ImageCodec.FormatOf("a.tif"));
        Assert.Equal(ImageFormat.Tiff, ImageCodec.FormatOf("a.tiff"));
        Assert.Equal(ImageFormat.Ico, ImageCodec.FormatOf("a.ico"));
        Assert.Equal(ImageFormat.Webp, ImageCodec.FormatOf("a.webp"));
        Assert.Equal(ImageFormat.Bmp, ImageCodec.FormatOf("a.bmp"));
        Assert.Null(ImageCodec.FormatOf("a.heic"));
        Assert.Null(ImageCodec.FormatOf("a.heif"));
        Assert.Null(ImageCodec.FormatOf("a.paint"));
    }

    [Theory]
    [InlineData(ImageFormat.Ico)]
    [InlineData(ImageFormat.Webp)]
    public void OpenOnlyFormats_CannotBeExported(ImageFormat format)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Export(Gradient(), format));
    }

    [Theory]
    [InlineData("a.bmp", ImageFormat.Bmp256, ImageFormat.Bmp256)] // a chosen BMP variant is kept
    [InlineData("a.dib", ImageFormat.BmpMonochrome, ImageFormat.BmpMonochrome)]
    [InlineData("a.bmp", null, ImageFormat.Bmp)]
    [InlineData("a.gif", ImageFormat.Bmp16, ImageFormat.Gif)] // the extension wins over the chosen type
    [InlineData("a.tiff", null, ImageFormat.Tiff)]
    [InlineData("a.webp", null, null)] // open-only formats are never exported
    [InlineData("a.ico", ImageFormat.Ico, null)]
    [InlineData("a.heic", ImageFormat.Png, null)]
    [InlineData("noextension", ImageFormat.Png, null)]
    public void ExportFormatOf_PrefersTheChosenTypeWhenTheExtensionFits(string name, ImageFormat? chosen, ImageFormat? expected)
    {
        Assert.Equal(expected, ImageCodec.ExportFormatOf(name, chosen));
    }

    [Fact]
    public void Gif_RoundTripsAnImageOf256ColoursExactly()
    {
        var document = Stripes(256);

        var file = Export(document, ImageFormat.Gif);

        Assert.Equal("GIF89a"u8.ToArray(), file[..6]);
        Assert.Equal(Composite(document), Composite(Open(file)));
    }

    [Fact]
    public void Tiff_RoundTripsOpaquePixelsExactly()
    {
        var document = Gradient(40, 30);

        var file = Export(document, ImageFormat.Tiff);

        Assert.Equal("II*\0"u8.ToArray(), file[..4]);
        Assert.Equal(Composite(document), Composite(Open(file)));
    }

    [Theory]
    [InlineData(ImageFormat.Bmp256, 8)]
    [InlineData(ImageFormat.Bmp16, 4)]
    [InlineData(ImageFormat.BmpMonochrome, 1)]
    public void LowColourBmp_HasThePaletteBitDepth(ImageFormat format, short bitsPerPixel)
    {
        var file = Export(Gradient(), format);

        Assert.Equal("BM"u8.ToArray(), file[..2]);
        Assert.Equal(bitsPerPixel, BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(28)));
        Assert.Equal((32, 24), (Open(file).Width, Open(file).Height));
    }

    [Fact]
    public void Bmp256_RoundTripsAnImageOf256ColoursExactly()
    {
        var document = Stripes(256);

        Assert.Equal(Composite(document), Composite(Open(Export(document, ImageFormat.Bmp256))));
    }

    [Theory]
    [InlineData(ImageFormat.Gif)]
    [InlineData(ImageFormat.Bmp256)]
    public void PaletteFormats_QuantiseImagesWithMoreColours(ImageFormat format)
    {
        var document = Gradient(128, 128); // ~16 000 colours

        var reopened = Composite(Open(Export(document, format)));

        Assert.InRange(reopened.Distinct().Count(), 2, 256);
        Assert.Equal(128 * 128, reopened.Length);
    }

    [Fact]
    public void Bmp16_UsesOnlyTheStandardSixteenColours()
    {
        var reopened = Composite(Open(Export(Gradient(64, 64), ImageFormat.Bmp16)));

        Assert.All(reopened, p => Assert.Contains(p, SixteenColours));
        Assert.True(reopened.Distinct().Count() > 2);
    }

    [Fact]
    public void BmpMonochrome_UsesOnlyBlackAndWhite()
    {
        var document = Gradient(64, 64);
        document.ActiveLayer.Pixels.SetPixel(0, 0, Rgba.Black);
        document.ActiveLayer.Pixels.SetPixel(63, 63, Rgba.White);

        var reopened = Composite(Open(Export(document, ImageFormat.BmpMonochrome)));

        Assert.All(reopened, p => Assert.True(p == Rgba.Black || p == Rgba.White, p.ToString()));
        Assert.Equal(Rgba.Black, reopened[0]);
        Assert.Equal(Rgba.White, reopened[^1]);
    }

    [Theory]
    [InlineData(ImageFormat.Tiff)]
    [InlineData(ImageFormat.Gif)]
    [InlineData(ImageFormat.Bmp256)]
    [InlineData(ImageFormat.Bmp16)]
    [InlineData(ImageFormat.BmpMonochrome)]
    public void FormatsWithoutAlpha_FlattenTransparencyOntoWhite(ImageFormat format)
    {
        var document = Document.CreateBlank(16, 16);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(0, 0, 16, 16), new Rgba[16 * 16]);

        var reopened = Open(Export(document, format));

        Assert.All(Composite(reopened), p => Assert.Equal(Rgba.White, p));
    }

    [Fact]
    public void AnimatedGif_OpensAsItsFirstFrameOnly()
    {
        var file = Gif(8, 6, Red, Blue, Blue);

        var document = Open(file);

        Assert.Single(document.Layers);
        Assert.Equal((8, 6), (document.Width, document.Height));
        Assert.All(Composite(document), p => Assert.Equal(new Rgba(255, 0, 0, 255), p));
    }

    [Fact]
    public void MultiPageTiff_OpensAsItsFirstPageOnly()
    {
        var document = Open(Tiff(8, 6, Blue, Red));

        Assert.Single(document.Layers);
        Assert.All(Composite(document), p => Assert.Equal(new Rgba(0, 0, 255, 255), p));
    }

    [Fact]
    public void Ico_Opens()
    {
        var document = Open(Ico(32, 32, new Rgba32(0, 128, 0, 255)));

        Assert.Equal((32, 32), (document.Width, document.Height));
        Assert.All(Composite(document), p => Assert.Equal(new Rgba(0, 128, 0, 255), p));
    }

    [Fact]
    public void Webp_OpensKeepingTransparency()
    {
        var document = Open(Webp(20, 10, new Rgba32(0, 0, 255, 0)));

        Assert.Equal((20, 10), (document.Width, document.Height));
        Assert.All(Composite(document), p => Assert.Equal(Rgba.Transparent, p));
        Assert.All(Composite(Open(Webp(4, 4, Red))), p => Assert.Equal(new Rgba(255, 0, 0, 255), p));
    }

    /// <summary>
    /// These headers are followed by no usable pixel data, so decoding would fail as unreadable.
    /// Getting the size failure instead shows the header was checked before decoding.
    /// </summary>
    [Theory]
    [InlineData("gif-side", OpenFailure.TooLarge)]
    [InlineData("gif-budget", OpenFailure.OverMemoryBudget)]
    [InlineData("tiff-side", OpenFailure.TooLarge)]
    [InlineData("tiff-budget", OpenFailure.OverMemoryBudget)]
    [InlineData("webp-side", OpenFailure.TooLarge)]
    [InlineData("webp-budget", OpenFailure.OverMemoryBudget)]
    [InlineData("ico-png-side", OpenFailure.TooLarge)]
    [InlineData("ico-png-budget", OpenFailure.OverMemoryBudget)]
    public void Open_ChecksHeaderSizeBeforeDecoding(string sample, OpenFailure expected)
    {
        var file = sample switch
        {
            "gif-side" => GifHeader(20000, 20000, frames: 1),
            "gif-budget" => GifHeader(16000, 16000, frames: 1),
            "tiff-side" => TiffHeader(20000, 20000, [1, 2, 3]),
            "tiff-budget" => TiffHeader(16000, 16000, [1, 2, 3]),
            "webp-side" => WebpCanvas(20000, 20000),
            "webp-budget" => WebpLossless(16000, 16000, [1, 2, 3]),
            "ico-png-side" => IcoHolding(Png(20000, 20000, [1, 2, 3])),
            _ => IcoHolding(Png(16000, 16000, [1, 2, 3])),
        };

        var error = Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.True(expected == error.Failure, $"{error.Failure}: {error.InnerException}");
    }
}
