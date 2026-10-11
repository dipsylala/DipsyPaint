using System.Diagnostics;
using DipsyPaint.Engine.Codecs;
using SixLabors.ImageSharp.PixelFormats;
using static DipsyPaint.Engine.Tests.Codecs.FormatSamples;
using static DipsyPaint.Engine.Tests.Codecs.SampleFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>
/// Malicious-sample corpus for flat formats (ADR-0014, AC18–AC21). Samples are generated here rather
/// than committed as files. Every one must fail with <see cref="ImageOpenException"/>, quickly.
/// </summary>
public class MaliciousSampleTests
{
    private static readonly Dictionary<string, Func<byte[]>> Samples = new()
    {
        ["empty"] = () => [],
        ["garbage"] = () => Garbage(4096),
        ["text-file"] = () => "This is not an image."u8.ToArray(),
        ["gif-no-image"] = () => "GIF89a\x01\x00\x01\x00\x00\x00\x00;"u8.ToArray(),
        ["unsupported-tga"] = () => [0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 24, 0, 0, 0, 255],
        ["png-signature-only"] = () => Png(1, 1)[..8],
        ["png-wrong-magic"] = () => [.. "\x89PNX"u8, .. Png(4, 4, Zlib(new byte[4 * 17]))[4..]],
        ["png-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Png)),
        ["png-truncated-in-header"] = () => Png(4, 4)[..20],
        ["png-missing-pixel-data"] = () => Png(4, 4),
        ["png-zero-size"] = () => Png(0, 0, Zlib([0])),
        ["png-negative-size"] = () => Png(-1, 10, Zlib([0])),
        ["png-oversized-header"] = () => Png(int.MaxValue, int.MaxValue, Zlib([0])),
        ["png-bomb-header"] = () => Png(Document.MaxSide, Document.MaxSide, Zlib(new byte[1 << 20])),
        ["png-bad-header-crc"] = () => Png(4, 4, Zlib(new byte[4 * 17]), breakCrc: true),
        ["png-garbage-pixel-data"] = () => Png(4, 4, Garbage(64)),
        ["jpeg-soi-only"] = () => [0xFF, 0xD8],
        ["jpeg-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Jpeg)),
        ["jpeg-header-only"] = () => JpegHeader(32, 32),
        ["jpeg-zero-size"] = () => JpegDeclaring(0, 0),
        ["jpeg-oversized-header"] = () => JpegDeclaring(65535, 65535),
        ["jpeg-bomb-header"] = () => JpegDeclaring(16000, 16000),
        ["jpeg-garbage-after-soi"] = () => [0xFF, 0xD8, .. Garbage(512)],
        ["bmp-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Bmp)),
        ["bmp-header-only"] = () => BmpHeader(32, 32),
        ["bmp-zero-size"] = () => BmpHeader(0, 0),
        ["bmp-oversized-header"] = () => BmpHeader(int.MaxValue, int.MaxValue),
        ["bmp-min-int-height"] = () => BmpHeader(16, int.MinValue),
        ["bmp-bad-bit-depth"] = () => [.. BmpHeader(4, 4, bitsPerPixel: 7), .. new byte[64]],
        ["bmp-wrong-magic"] = () => [(byte)'X', (byte)'M', .. Export(Gradient(), ImageFormat.Bmp)[2..]],
        ["bmp-256-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Bmp256)),
        ["gif-signature-only"] = () => "GIF89a"u8.ToArray(),
        ["gif-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Gif)),
        ["gif-zero-size"] = () => GifHeader(0, 0, frames: 1),
        ["gif-oversized-header"] = () => GifHeader(65535, 65535, frames: 1),
        ["gif-bomb-header"] = () => GifHeader(16000, 16000, frames: 1),
        ["gif-many-frames-bomb-header"] = () => GifHeader(16000, 16000, frames: 200_000),
        ["tiff-header-only"] = () => "II*\0"u8.ToArray(),
        ["tiff-wrong-magic"] = () => [(byte)'I', (byte)'I', 43, 0, .. Tiff(8, 8, new Rgba32(1, 2, 3))[4..]],
        ["tiff-truncated-half"] = () => Half(Export(Gradient(), ImageFormat.Tiff)),
        ["tiff-zero-size"] = () => TiffHeader(0, 0, new byte[16]),
        ["tiff-oversized-header"] = () => TiffHeader(uint.MaxValue, uint.MaxValue, new byte[16]),
        ["tiff-bomb-header"] = () => TiffHeader(16000, 16000, new byte[16]),
        ["tiff-huge-strip-byte-count"] = () => TiffHeader(64, 64, new byte[16], stripByteCount: uint.MaxValue),
        ["tiff-ifd-past-end"] = () => [.. "II*\0"u8, 0xF0, 0xFF, 0xFF, 0x7F],
        ["tiff-ifd-loop"] = () => TiffHeader(4, 4, new byte[16], nextIfd: 8),
        ["ico-header-only"] = () => [0, 0, 1, 0, 1, 0],
        ["ico-no-entries"] = () => IcoHolding([], entries: 0),
        ["ico-truncated-half"] = () => Half(Ico(32, 32, new Rgba32(1, 2, 3, 255))),
        ["ico-image-past-end"] = () => IcoHolding(Png(4, 4, Zlib(new byte[4 * 17])), imageOffset: 0x7FFF_0000),
        ["ico-png-oversized"] = () => IcoHolding(Png(65535, 65535, Zlib([0]))),
        ["ico-png-bomb"] = () => IcoHolding(Png(16000, 16000, Zlib(new byte[1 << 16]))),
        ["ico-bmp-oversized"] = () => IcoHolding(BmpHeader(100000, 200000, bitsPerPixel: 32)[14..]),
        ["ico-garbage-image"] = () => IcoHolding(Garbage(512)),
        ["webp-riff-only"] = () => "RIFF\x04\x00\x00\x00WEBP"u8.ToArray(),
        ["webp-truncated-half"] = () => Half(Webp(32, 32, new Rgba32(1, 2, 3, 255))),
        ["webp-canvas-zero-data"] = () => WebpCanvas(32, 32),
        ["webp-canvas-oversized"] = () => WebpCanvas(1 << 24, 1 << 24),
        ["webp-lossless-header-only"] = () => WebpLossless(64, 64),
        ["webp-lossless-bomb-header"] = () => WebpLossless(16000, 16000, Garbage(64)),
        ["webp-lossless-garbage"] = () => WebpLossless(64, 64, Garbage(4096)),
        ["webp-riff-size-lie"] = () => WebpLossless(64, 64, Garbage(64), riffSize: uint.MaxValue),
    };

    public static TheoryData<string> SampleNames => [.. Samples.Keys];

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void MaliciousSample_FailsCleanlyAsCouldNotOpen(string name)
    {
        var file = Samples[name]();
        var clock = Stopwatch.StartNew();

        Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{name} took {clock.Elapsed}");
    }

    /// <summary>
    /// A well-formed compressed stream that simply ends early is not detectable as damage; like browsers,
    /// ImageSharp opens it with the missing rows blank. It must still open as a complete document.
    /// </summary>
    [Fact]
    public void ShortButWellFormedPngPixelData_OpensWithMissingRowsBlank()
    {
        var document = Open(Png(64, 64, Zlib(new byte[10])));

        Assert.Equal((64, 64), (document.Width, document.Height));
        Assert.Equal(Rgba.Transparent, document.ActiveLayer.Pixels.GetPixel(63, 63));
    }

    /// <summary>
    /// Damaged files that ImageSharp tolerates, as browsers do: bad GIF pixel data and TIFF strips that
    /// are missing or point past the end decode as blank or noise. Each must either fail as "could not
    /// open" or open, quickly, as a complete document of the size its header declares (ADR-0014).
    /// </summary>
    private static readonly Dictionary<string, (Func<byte[]> File, int Width, int Height)> Tolerated = new()
    {
        // The frame is clipped to the logical screen, so its huge declared size is never allocated.
        ["gif-frame-overflowing-screen"] = (() => GifFrame(4, 4, 65535, 65535, [0x02, 0xFF, .. Garbage(255), 0x00]), 4, 4),
        ["gif-bad-lzw-code-size"] = (() => GifFrame(4, 4, 4, 4, [0x0F, 0x02, 0xFF, 0xFF, 0x00]), 4, 4),
        ["gif-garbage-pixel-data"] = (() => GifFrame(16, 16, 16, 16, [0x08, 0xFF, .. Garbage(255), 0xFF, .. Garbage(255), 0x00]), 16, 16),
        ["gif-many-frames"] = (() => GifHeader(8, 8, frames: 200_000), 8, 8),
        ["tiff-no-strips"] = (() => TiffHeader(32, 32), 32, 32),
        ["tiff-strip-past-end"] = (() => TiffHeader(64, 64, new byte[16], stripOffset: 0x7FFF_0000, stripByteCount: 4096), 64, 64),
        ["tiff-huge-rows-per-strip"] = (() => TiffHeader(64, 64, new byte[64 * 64], rowsPerStrip: 0x7FFF_FFFF), 64, 64),
    };

    public static TheoryData<string> ToleratedNames => [.. Tolerated.Keys];

    [Theory]
    [MemberData(nameof(ToleratedNames))]
    public void ToleratedSample_OpensCompletelyOrFailsCleanly(string name)
    {
        var (file, width, height) = Tolerated[name];
        var bytes = file();
        var clock = Stopwatch.StartNew();

        try
        {
            var document = Open(bytes);
            Assert.Equal((width, height), (document.Width, document.Height));
        }
        catch (ImageOpenException)
        {
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{name} took {clock.Elapsed}");
    }

    private static byte[] Garbage(int length)
    {
        var bytes = new byte[length];
        new Random(1234).NextBytes(bytes);
        return bytes;
    }

    private static byte[] Half(byte[] file) => file[..(file.Length / 2)];
}
