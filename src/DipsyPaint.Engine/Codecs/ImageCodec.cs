using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

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
        new PngConfigurationModule(), new JpegConfigurationModule(), new BmpConfigurationModule(),
        new GifConfigurationModule(), new TiffConfigurationModule(), new IcoConfigurationModule(),
        new WebpConfigurationModule());

    private static readonly DecoderOptions Decoding = new()
    {
        Configuration = Formats,
        MaxFrames = 1,
        // No colour management (ADR-0005), so metadata is never needed; skipping it also skips its parsers.
        SkipMetadata = true,
    };

    // The standard Windows 16-colour palette, which 16-colour bitmaps use.
    private static readonly Color[] SixteenColours =
    [
        Rgb(0x00, 0x00, 0x00), Rgb(0x80, 0x00, 0x00), Rgb(0x00, 0x80, 0x00), Rgb(0x80, 0x80, 0x00),
        Rgb(0x00, 0x00, 0x80), Rgb(0x80, 0x00, 0x80), Rgb(0x00, 0x80, 0x80), Rgb(0xC0, 0xC0, 0xC0),
        Rgb(0x80, 0x80, 0x80), Rgb(0xFF, 0x00, 0x00), Rgb(0x00, 0xFF, 0x00), Rgb(0xFF, 0xFF, 0x00),
        Rgb(0x00, 0x00, 0xFF), Rgb(0xFF, 0x00, 0xFF), Rgb(0x00, 0xFF, 0xFF), Rgb(0xFF, 0xFF, 0xFF),
    ];

    private static readonly Color[] BlackAndWhite = [Rgb(0x00, 0x00, 0x00), Rgb(0xFF, 0xFF, 0xFF)];

    /// <summary>File extensions for each format, lower case with the leading dot; the first is the default.</summary>
    public static IReadOnlyList<string> Extensions(ImageFormat format) => format switch
    {
        ImageFormat.Png => [".png"],
        ImageFormat.Jpeg => [".jpg", ".jpeg", ".jpe", ".jfif"],
        ImageFormat.Bmp or ImageFormat.Bmp256 or ImageFormat.Bmp16 or ImageFormat.BmpMonochrome => [".bmp", ".dib"],
        ImageFormat.Gif => [".gif"],
        ImageFormat.Tiff => [".tif", ".tiff"],
        ImageFormat.Ico => [".ico"],
        ImageFormat.Webp => [".webp"],
        ImageFormat.OpenRaster => [".ora"],
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>Whether <see cref="Export"/> can write <paramref name="format"/>; ICO and WebP only open (ADR-0019).</summary>
    public static bool CanExport(ImageFormat format) => format is not (ImageFormat.Ico or ImageFormat.Webp);

    /// <summary>
    /// Whether exporting to <paramref name="format"/> reduces the image to a palette of 256 colours or
    /// fewer, losing colour and transparency; reference Paint warns before such a save.
    /// </summary>
    public static bool UsesPalette(ImageFormat format) =>
        format is ImageFormat.Gif or ImageFormat.Bmp256 or ImageFormat.Bmp16 or ImageFormat.BmpMonochrome;

    /// <summary>
    /// The format implied by <paramref name="fileName"/>'s extension, or null if none matches.
    /// A BMP extension gives <see cref="ImageFormat.Bmp"/> (24-bit).
    /// </summary>
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
    /// The format to export <paramref name="fileName"/> as: <paramref name="chosen"/> when the file's
    /// extension belongs to it (so a chosen BMP variant is kept), else the exportable format the
    /// extension implies, else null.
    /// </summary>
    public static ImageFormat? ExportFormatOf(string fileName, ImageFormat? chosen = null)
    {
        if (chosen is { } format && CanExport(format)
            && Extensions(format).Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        {
            return format;
        }

        return FormatOf(fileName) is { } implied && CanExport(implied) ? implied : null;
    }

    /// <summary>
    /// Decodes a PNG, JPEG, BMP, GIF, TIFF, ICO or WebP into a new one-layer document, detecting the
    /// format by content. Only the first frame of an animated or multi-page image is read. The size
    /// declared in the header is checked against <see cref="Document.MaxSide"/> and
    /// <paramref name="budget"/> before any pixels are decoded. A ZIP archive is read as an OpenRaster
    /// project file, keeping its layers (<see cref="OpenRasterCodec.Open"/>).
    /// </summary>
    /// <exception cref="ImageOpenException">The file could not be opened, for any reason.</exception>
    public static Document Open(Stream stream, MemoryBudget? budget = null)
    {
        budget ??= MemoryBudget.Default;
        try
        {
            var source = stream.CanSeek ? stream : CopyToMemory(stream);
            var start = source.Position;
            Span<byte> signature = stackalloc byte[4];
            var isZip = source.ReadAtLeast(signature, 4, throwOnEndOfStream: false) == 4 && signature.SequenceEqual("PK\x03\x04"u8);
            source.Position = start;
            if (isZip)
            {
                return OpenRasterCodec.Open(source, budget);
            }

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
            throw new ImageOpenException(OpenFailure.Unreadable, "The file is not a readable image in a supported format.", e);
        }
    }

    /// <summary>
    /// Writes the composited document to <paramref name="stream"/>; layers are not kept, except by
    /// <see cref="ImageFormat.OpenRaster"/>, the project format (<see cref="OpenRasterCodec.Save"/>,
    /// which needs a seekable stream).
    /// </summary>
    /// <remarks>
    /// Only PNG keeps alpha (ImageSharp writes TIFF without it); for the other formats the composite is
    /// flattened onto white first.
    /// Palette formats (<see cref="UsesPalette"/>) are quantised with dithering: 256-colour BMP and GIF
    /// to a palette fitted to the image, 16-colour BMP to the standard Windows 16 colours, and
    /// monochrome BMP to black and white.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> cannot be exported (<see cref="CanExport"/>).</exception>
    public static void Export(Compositor compositor, Stream stream, ImageFormat format)
    {
        if (format == ImageFormat.OpenRaster)
        {
            OpenRasterCodec.Save(compositor, stream);
            return;
        }

        ImageEncoder encoder = format switch
        {
            ImageFormat.Png => new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8 },
            // 90 is the Windows imaging (WIC) default quality.
            ImageFormat.Jpeg => new JpegEncoder { Quality = 90 },
            ImageFormat.Bmp => new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit24 },
            ImageFormat.Bmp256 => new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit8, Quantizer = new WuQuantizer() },
            ImageFormat.Bmp16 => new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit4, Quantizer = new PaletteQuantizer(SixteenColours) },
            ImageFormat.BmpMonochrome => new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit1, Quantizer = new PaletteQuantizer(BlackAndWhite) },
            ImageFormat.Gif => new GifEncoder { Quantizer = new WuQuantizer() },
            ImageFormat.Tiff => new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit24 },
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "This format cannot be exported."),
        };
        var background = format == ImageFormat.Png ? (Rgba?)null : Rgba.White;
        using var image = PixelBridge.ToImage(compositor, Formats, background);
        image.Save(stream, encoder);
    }

    /// <summary>
    /// Throws an <see cref="ImageOpenException"/> unless an image of this size fits within
    /// <see cref="Document.MaxSide"/> and, at <paramref name="costPerPixel"/> bytes per pixel, the budget.
    /// </summary>
    internal static void CheckSize(int width, int height, MemoryBudget budget, long costPerPixel = OpenCostPerPixel)
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

        if (!budget.Allows((long)width * height * costPerPixel))
        {
            throw new ImageOpenException(OpenFailure.OverMemoryBudget,
                $"A {width}×{height} px image needs more memory than the budget allows.");
        }
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromPixel(new Rgb24(r, g, b));

    private static MemoryStream CopyToMemory(Stream stream)
    {
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
