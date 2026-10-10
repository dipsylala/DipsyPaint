using System.Diagnostics;
using DipsyPaint.Engine.Codecs;
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
        ["unsupported-gif"] = () => "GIF89a\x01\x00\x01\x00\x00\x00\x00;"u8.ToArray(),
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

    private static byte[] Garbage(int length)
    {
        var bytes = new byte[length];
        new Random(1234).NextBytes(bytes);
        return bytes;
    }

    private static byte[] Half(byte[] file) => file[..(file.Length / 2)];
}
