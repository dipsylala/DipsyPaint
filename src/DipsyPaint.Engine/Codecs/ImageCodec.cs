using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// Opens and exports flat image files. All encoding and decoding goes through ImageSharp (ADR-0008);
/// files are treated as untrusted (ADR-0014).
/// </summary>
public static class ImageCodec
{
    /// <summary>
    /// Estimated bytes per pixel needed to open an image: the decoded image, the layer, the composite
    /// and the shell's on-screen bitmap, 4 bytes each.
    /// </summary>
    public const long OpenCostPerPixel = 16;

    // Only the formats this build supports, so nothing else is ever parsed.
    private static readonly Configuration Formats = new(
        new PngConfigurationModule(), new JpegConfigurationModule(), new BmpConfigurationModule());

    private static readonly DecoderOptions Decoding = new()
    {
        Configuration = Formats,
        MaxFrames = 1,
        // No colour management (ADR-0005), so metadata is never needed; skipping it also skips its parsers.
        SkipMetadata = true,
    };

    /// <summary>File extensions for each format, lower case with the leading dot; the first is the default.</summary>
    public static IReadOnlyList<string> Extensions(ImageFormat format) => format switch
    {
        ImageFormat.Png => [".png"],
        ImageFormat.Jpeg => [".jpg", ".jpeg", ".jpe", ".jfif"],
        ImageFormat.Bmp => [".bmp", ".dib"],
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>The format implied by <paramref name="fileName"/>'s extension, or null if none matches.</summary>
    public static ImageFormat? FormatOf(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        foreach (var format in Enum.GetValues<ImageFormat>())
        {
            if (Extensions(format).Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        return null;
    }

    /// <summary>
    /// Decodes a PNG, JPEG or BMP into a new one-layer document. The size declared in the header is
    /// checked against <see cref="Document.MaxSide"/> and <paramref name="budget"/> before any pixels
    /// are decoded.
    /// </summary>
    /// <exception cref="ImageOpenException">The file could not be opened, for any reason.</exception>
    public static Document Open(Stream stream, MemoryBudget? budget = null)
    {
        budget ??= MemoryBudget.Default;
        try
        {
            var source = stream.CanSeek ? stream : CopyToMemory(stream);
            var start = source.Position;
            var info = Image.Identify(Decoding, source);
            CheckSize(info.Width, info.Height, budget);

            source.Position = start;
            using var image = Image.Load<Rgba32>(Decoding, source);
            CheckSize(image.Width, image.Height, budget);
            return PixelBridge.ToDocument(image);
        }
        catch (Exception e) when (e is not ImageOpenException)
        {
            // Any decoder failure, including running out of memory, means "could not open" (ADR-0014).
            throw new ImageOpenException(OpenFailure.Unreadable, "The file is not a readable PNG, JPEG or BMP image.", e);
        }
    }

    /// <summary>Writes the composited document to <paramref name="stream"/>; layers are not kept.</summary>
    /// <remarks>JPEG and BMP have no alpha, so the composite is flattened onto white first.</remarks>
    public static void Export(Compositor compositor, Stream stream, ImageFormat format)
    {
        ImageEncoder encoder = format switch
        {
            ImageFormat.Png => new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8 },
            // 90 is the Windows imaging (WIC) default quality.
            ImageFormat.Jpeg => new JpegEncoder { Quality = 90 },
            ImageFormat.Bmp => new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit24 },
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        var background = format == ImageFormat.Png ? (Rgba?)null : Rgba.White;
        using var image = PixelBridge.ToImage(compositor, Formats, background);
        image.Save(stream, encoder);
    }

    private static void CheckSize(int width, int height, MemoryBudget budget)
    {
        if (width < 1 || height < 1)
        {
            throw new ImageOpenException(OpenFailure.Unreadable, $"The image declares an empty size ({width}×{height}).");
        }

        if (width > Document.MaxSide || height > Document.MaxSide)
        {
            throw new ImageOpenException(OpenFailure.TooLarge,
                $"The image is {width}×{height} px; documents are limited to {Document.MaxSide} px per side.");
        }

        if (!budget.Allows((long)width * height * OpenCostPerPixel))
        {
            throw new ImageOpenException(OpenFailure.OverMemoryBudget,
                $"Opening a {width}×{height} px image needs more memory than the budget allows.");
        }
    }

    private static MemoryStream CopyToMemory(Stream stream)
    {
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
