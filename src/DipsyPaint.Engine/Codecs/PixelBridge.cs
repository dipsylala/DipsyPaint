using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// The single conversion point between ImageSharp pixels (straight alpha) and engine pixels
/// (premultiplied <see cref="Rgba"/>, Skia's native format), ADR-0005 and ADR-0008.
/// No other engine code touches ImageSharp pixel types.
/// </summary>
internal static class PixelBridge
{
    /// <summary>Creates a one-layer document holding <paramref name="image"/>'s pixels.</summary>
    public static Document ToDocument(Image<Rgba32> image)
    {
        var document = Document.CreateBlank(image.Width, image.Height);
        var layer = document.ActiveLayer.Pixels;
        var row = new Rgba[image.Width];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var source = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = Premultiply(source[x]);
                }

                layer.WriteRegion(new IntRect(0, y, row.Length, 1), row);
            }
        });
        return document;
    }

    /// <summary>Copies <paramref name="image"/>'s pixels into a new <see cref="PixelImage"/>.</summary>
    public static PixelImage ToPixelImage(Image<Rgba32> image)
    {
        var pixels = new Rgba[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var source = accessor.GetRowSpan(y);
                var target = pixels.AsSpan(y * image.Width, image.Width);
                for (var x = 0; x < target.Length; x++)
                {
                    target[x] = Premultiply(source[x]);
                }
            }
        });
        return new PixelImage(image.Width, image.Height, pixels);
    }

    /// <summary>Copies <paramref name="pixels"/> into a new straight-alpha image.</summary>
    public static Image<Rgba32> ToImage(PixelImage pixels, Configuration configuration)
    {
        var image = new Image<Rgba32>(configuration, pixels.Width, pixels.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var target = accessor.GetRowSpan(y);
                var source = pixels.Pixels.AsSpan(y * pixels.Width, pixels.Width);
                for (var x = 0; x < target.Length; x++)
                {
                    target[x] = Unpremultiply(source[x]);
                }
            }
        });
        return image;
    }

    /// <summary>
    /// Copies the composite into a new straight-alpha image, first compositing it over
    /// <paramref name="background"/> when given (for formats without alpha).
    /// </summary>
    public static Image<Rgba32> ToImage(Compositor compositor, Configuration configuration, Rgba? background)
    {
        var image = new Image<Rgba32>(configuration, compositor.Width, compositor.Height);
        image.ProcessPixelRows(accessor =>
        {
            var composite = compositor.Pixels;
            for (var y = 0; y < accessor.Height; y++)
            {
                var target = accessor.GetRowSpan(y);
                var source = composite.Slice(y * compositor.Width, compositor.Width);
                for (var x = 0; x < target.Length; x++)
                {
                    target[x] = Unpremultiply(background is { } b ? source[x].Over(b) : source[x]);
                }
            }
        });
        return image;
    }

    private static Rgba Premultiply(Rgba32 p) =>
        new((byte)MultiplyAlpha(p.R, p.A), (byte)MultiplyAlpha(p.G, p.A), (byte)MultiplyAlpha(p.B, p.A), p.A);

    private static Rgba32 Unpremultiply(Rgba p) => p.A == 0
        ? default
        : new Rgba32(DivideAlpha(p.R, p.A), DivideAlpha(p.G, p.A), DivideAlpha(p.B, p.A), p.A);

    // Rounded c × a / 255. Rounding both ways makes premultiplied → straight → premultiplied exact.
    private static int MultiplyAlpha(int c, int a) => (c * a + 127) / 255;

    private static byte DivideAlpha(int c, int a) => (byte)Math.Min(255, (c * 255 + a / 2) / a);
}
