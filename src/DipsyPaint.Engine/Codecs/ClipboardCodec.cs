using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace DipsyPaint.Engine.Codecs;

/// <summary>How image data on the clipboard is encoded.</summary>
public enum ClipboardEncoding
{
    Png,

    /// <summary>A Windows device-independent bitmap (<c>CF_DIB</c>): a BMP file without its 14-byte file header.</summary>
    Dib,

    Tiff,
}

/// <summary>
/// Encodes and decodes clipboard image data through ImageSharp (ADR-0008). Clipboard data comes from
/// any app, so it is untrusted, like a file (ADR-0014): only the encoding it is labelled with is
/// parsed, and the size its header declares is checked before any pixels are decoded.
/// </summary>
public static class ClipboardCodec
{
    /// <summary>
    /// Estimated bytes per pixel needed to paste an image: the decoded image, the pasted pixels, a
    /// stretched copy of them and the history step's before-pixels on commit, 4 bytes each.
    /// </summary>
    public const long PasteCostPerPixel = 16;

    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const uint BitFields = 3;
    private const uint AlphaBitFields = 6;

    private static readonly Configuration Encoding = new(new PngConfigurationModule());
    private static readonly PngEncoder Png = new() { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8 };

    /// <summary>Encodes <paramref name="image"/> as PNG, keeping its alpha.</summary>
    public static byte[] EncodePng(PixelImage image)
    {
        using var encoded = PixelBridge.ToImage(image, Encoding);
        using var stream = new MemoryStream();
        encoded.Save(stream, Png);
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes clipboard data labelled <paramref name="encoding"/>. The declared size is checked
    /// against <see cref="Document.MaxSide"/> and <paramref name="budget"/> first.
    /// </summary>
    /// <exception cref="ImageOpenException">The data could not be decoded, for any reason.</exception>
    public static PixelImage Decode(byte[] data, ClipboardEncoding encoding, MemoryBudget? budget = null)
    {
        budget ??= MemoryBudget.Default;
        try
        {
            using var stream = new MemoryStream(encoding == ClipboardEncoding.Dib ? DibToBmp(data) : data, writable: false);
            var options = OptionsFor(encoding);
            var info = Image.Identify(options, stream);
            ImageCodec.CheckSize(info.Width, info.Height, budget, PasteCostPerPixel);

            stream.Position = 0;
            using var image = Image.Load<Rgba32>(options, stream);
            ImageCodec.CheckSize(image.Width, image.Height, budget, PasteCostPerPixel);
            return PixelBridge.ToPixelImage(image);
        }
        catch (Exception e) when (e is not ImageOpenException)
        {
            throw new ImageOpenException(OpenFailure.Unreadable, $"The clipboard data is not a readable {encoding} image.", e);
        }
    }

    /// <summary>
    /// Checks that an image of this size may be pasted: within <see cref="Document.MaxSide"/> and
    /// <paramref name="budget"/>. For images the platform hands over as pixels rather than encoded data.
    /// </summary>
    /// <exception cref="ImageOpenException">The image is too large.</exception>
    public static void CheckSize(int width, int height, MemoryBudget? budget = null) =>
        ImageCodec.CheckSize(width, height, budget ?? MemoryBudget.Default, PasteCostPerPixel);

    // Only the labelled format is ever parsed. No colour management (ADR-0005), so no metadata.
    private static DecoderOptions OptionsFor(ClipboardEncoding encoding) => new()
    {
        Configuration = new Configuration(encoding switch
        {
            ClipboardEncoding.Png => new PngConfigurationModule(),
            ClipboardEncoding.Dib => new BmpConfigurationModule(),
            ClipboardEncoding.Tiff => new TiffConfigurationModule(),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        }),
        MaxFrames = 1,
        SkipMetadata = true,
    };

    // Prepends the BMP file header, whose only real content is where the pixels start: after the
    // info header, the colour masks of a BITMAPINFOHEADER with bit fields, and the colour table.
    private static byte[] DibToBmp(byte[] dib)
    {
        if (dib.Length < InfoHeaderSize)
        {
            throw new ImageOpenException(OpenFailure.Unreadable, "The clipboard bitmap is truncated.");
        }

        var header = dib.AsSpan();
        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(header);
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        var coloursUsed = BinaryPrimitives.ReadUInt32LittleEndian(header[32..]);
        long masks = headerSize != InfoHeaderSize ? 0 : compression switch
        {
            BitFields => 12,
            AlphaBitFields => 16,
            _ => 0,
        };
        long colourTable = 4L * (coloursUsed != 0 ? coloursUsed : bitCount <= 8 ? 1u << bitCount : 0);
        var pixelOffset = FileHeaderSize + headerSize + masks + colourTable;
        if (headerSize < InfoHeaderSize || pixelOffset > FileHeaderSize + dib.Length)
        {
            throw new ImageOpenException(OpenFailure.Unreadable, "The clipboard bitmap has an invalid header.");
        }

        var bmp = new byte[FileHeaderSize + dib.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), (int)pixelOffset);
        dib.CopyTo(bmp, FileHeaderSize);
        return bmp;
    }
}
