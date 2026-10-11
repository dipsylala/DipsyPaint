using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Text;

/// <summary>
/// Renders the text tool's text with Avalonia's text layout (shaping, font fallback, wrapping) and
/// Skia. <see cref="TextEntry"/> styles its text box with the same <see cref="TypefaceOf"/> and
/// <see cref="FontSizeOf"/>, so the caret lines up with the rendered glyphs. Text is grayscale
/// anti-aliased, never sub-pixel, so no colour fringes end up in the image.
/// </summary>
public sealed class TextRenderer : ITextRenderer
{
    /// <summary>Document pixels per point: Paint sets type at 96 pixels per inch.</summary>
    public const double PixelsPerPoint = 96.0 / 72;

    public static FontFamily FontFamilyOf(TextStyle style) =>
        string.IsNullOrWhiteSpace(style.FontFamily) ? FontFamily.Default : new FontFamily(style.FontFamily);

    public static Typeface TypefaceOf(TextStyle style) =>
        new(FontFamilyOf(style), style.Italic ? FontStyle.Italic : FontStyle.Normal, style.Bold ? FontWeight.Bold : FontWeight.Normal);

    /// <summary>The font size in document pixels (device-independent pixels at 100% zoom).</summary>
    public static double FontSizeOf(TextStyle style) => style.Size * PixelsPerPoint;

    public static Color ColorOf(Rgba rgba)
    {
        var opaque = rgba.ToOpaque();
        return Color.FromArgb(rgba.A, opaque.R, opaque.G, opaque.B);
    }

    /// <summary>Lays the text out as the text box does: wrapped at <paramref name="width"/>, from the top left.</summary>
    public static TextLayout Layout(string text, TextStyle style, double width, IBrush? foreground)
    {
        var decorations = new TextDecorationCollection();
        if (style.Underline)
        {
            decorations.AddRange(TextDecorations.Underline);
        }

        if (style.Strikethrough)
        {
            decorations.AddRange(TextDecorations.Strikethrough);
        }

        return new TextLayout(
            text, TypefaceOf(style), FontSizeOf(style), foreground, TextAlignment.Left, TextWrapping.Wrap,
            textDecorations: decorations, maxWidth: width);
    }

    public PixelImage Render(TextRequest request)
    {
        var width = Math.Max(1, request.Width);
        using var layout = Layout(request.Text, request.Style, width, new SolidColorBrush(ColorOf(request.Foreground)));
        var height = Math.Max(Math.Max(1, request.MinHeight), (int)Math.Ceiling(layout.Height));
        var size = new PixelSize(width, height);

        using var target = new RenderTargetBitmap(size);
        using (var context = target.CreateDrawingContext())
        {
            if (request.Background is { } background)
            {
                context.FillRectangle(new SolidColorBrush(ColorOf(background)), new Rect(0, 0, width, height));
            }

            using (context.PushTextOptions(new Avalonia.Media.TextOptions
            {
                TextRenderingMode = TextRenderingMode.Antialias,
                TextHintingMode = TextHintingMode.Light,
                BaselinePixelAlignment = BaselinePixelAlignment.Aligned,
            }))
            {
                layout.Draw(context, default);
            }
        }

        // Converted to Rgba8888 premultiplied, the engine's pixel format.
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        using var buffer = converted.Lock();
        target.CopyPixels(buffer);
        var pixels = new Rgba[width * height];
        CopyRows(buffer, MemoryMarshal.AsBytes(pixels.AsSpan()), width * 4);
        return new PixelImage(width, height, pixels);
    }

    private static unsafe void CopyRows(ILockedFramebuffer buffer, Span<byte> destination, int rowLength)
    {
        for (var y = 0; y < buffer.Size.Height; y++)
        {
            new ReadOnlySpan<byte>((byte*)buffer.Address + y * buffer.RowBytes, rowLength).CopyTo(destination.Slice(y * rowLength));
        }
    }
}
