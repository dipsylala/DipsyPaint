using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Files;

namespace DipsyPaint;

/// <summary>
/// Exchanges images with other apps through the OS clipboard (Avalonia's clipboard API), per
/// ADR-0021. Encoded image data read from the clipboard is decoded by ImageSharp (ADR-0008) as
/// untrusted data (ADR-0014). DipsyPaint's own PNG also goes under its own format, so it pastes
/// losslessly into any DipsyPaint. For other apps:
/// <list type="bullet">
/// <item>Windows: writes an Avalonia bitmap, which Avalonia offers as PNG, CF_DIB, CF_DIBV5 and
/// CF_BITMAP (its Win32 clipboard serves those formats only from a bitmap, not from bytes we encode);
/// reads PNG, then CF_DIB or CF_DIBV5, then CF_BITMAP as pixels Avalonia gets from GDI.</item>
/// <item>macOS: writes <c>public.png</c>; reads <c>public.tiff</c>, which most apps offer. Avalonia
/// hands <c>public.png</c> over only as a bitmap it decoded itself (not through ImageSharp, without
/// size checks), so it is not read.</item>
/// <item>Linux: writes and reads <c>image/png</c>.</item>
/// </list>
/// </summary>
internal sealed class AvaloniaImageClipboard(Visual anchor) : IImageClipboard
{
    private static readonly DataFormat<byte[]> DipsyPaintPng = DataFormat.CreateBytesApplicationFormat("DipsyPaint.Png");

    // The platform's PNG format for other apps; on Windows only an Avalonia bitmap can be offered (see above).
    private static readonly DataFormat<byte[]>? PlatformPng =
        OperatingSystem.IsWindows() ? null : DataFormat.CreateBytesPlatformFormat(OperatingSystem.IsMacOS() ? "public.png" : "image/png");

    // Formats to paste from, most preferred first.
    private static readonly (DataFormat<byte[]> Format, ClipboardEncoding Encoding)[] Readable = ReadableFormats();

    private IClipboard Clipboard =>
        TopLevel.GetTopLevel(anchor)?.Clipboard ?? throw new InvalidOperationException("Not attached to a window with a clipboard.");

    public async Task SetImageAsync(PixelImage image)
    {
        var png = new Lazy<byte[]>(() => ClipboardCodec.EncodePng(image));
        var item = new DataTransferItem();
        item.Set(DipsyPaintPng, () => png.Value);
        if (PlatformPng is null)
        {
            item.Set(DataFormat.Bitmap, () => ToBitmap(image));
        }
        else
        {
            item.Set(PlatformPng, () => png.Value);
        }

        var data = new DataTransfer();
        data.Add(item);
        try
        {
            await Clipboard.SetDataAsync(data);
            // Windows only: renders the data now, so it stays on the clipboard after DipsyPaint closes, as Paint's does.
            await Clipboard.FlushAsync();
        }
        catch (Exception e) when (e is not IOException)
        {
            throw new IOException("The clipboard could not be written.", e);
        }
    }

    public async Task<PixelImage?> GetImageAsync()
    {
        Func<PixelImage>? decode;
        try
        {
            decode = await FindImageAsync();
        }
        catch (Exception e) when (e is not ImageOpenException)
        {
            throw new ImageOpenException(OpenFailure.Unreadable, "The clipboard could not be read.", e);
        }

        return decode is null ? null : await Task.Run(decode);
    }

    public Task ShowErrorAsync(string message) => new StorageProviderDialogs(anchor).ShowErrorAsync(message);

    // The most preferred image on the clipboard, as a function that decodes it (off the UI thread).
    private async Task<Func<PixelImage>?> FindImageAsync()
    {
        using var data = await Clipboard.TryGetDataAsync();
        if (data is null)
        {
            return null;
        }

        foreach (var (format, encoding) in Readable)
        {
            if (await data.TryGetValueAsync(format) is { } bytes)
            {
                return () => ClipboardCodec.Decode(bytes, encoding);
            }
        }

        // Windows: an image offered only as CF_BITMAP, a GDI bitmap. Avalonia hands it over only as
        // pixels it got from GDI (no codec involved), and the CF_DIB that Windows makes from it is not listed.
        if (OperatingSystem.IsWindows() && await data.TryGetBitmapAsync() is { } bitmap)
        {
            var image = FromGdiBitmap(bitmap);
            return () => image;
        }

        return null;
    }

    private static (DataFormat<byte[]>, ClipboardEncoding)[] ReadableFormats()
    {
        var formats = new List<(DataFormat<byte[]>, ClipboardEncoding)> { (DipsyPaintPng, ClipboardEncoding.Png) };
        if (OperatingSystem.IsWindows())
        {
            formats.Add((DataFormat.CreateBytesPlatformFormat("PNG"), ClipboardEncoding.Png));
            formats.Add((DataFormat.CreateBytesPlatformFormat("image/png"), ClipboardEncoding.Png));
            formats.Add((DataFormat.CreateBytesPlatformFormat("CF_DIB"), ClipboardEncoding.Dib));
            formats.Add((DataFormat.CreateBytesPlatformFormat("CF_DIBV5"), ClipboardEncoding.Dib));
        }
        else if (OperatingSystem.IsMacOS())
        {
            formats.Add((DataFormat.CreateBytesPlatformFormat("public.tiff"), ClipboardEncoding.Tiff));
        }
        else
        {
            formats.Add((PlatformPng!, ClipboardEncoding.Png));
        }

        return [.. formats];
    }

    // BGRA, because Avalonia writes CF_DIB as the bitmap's raw bytes labelled BI_RGB (blue first).
    // A plain pixel copy; no encoding happens here.
    private static unsafe Bitmap ToBitmap(PixelImage image)
    {
        var bgra = new byte[image.Pixels.Length * 4];
        for (var i = 0; i < image.Pixels.Length; i++)
        {
            var p = image.Pixels[i];
            (bgra[4 * i], bgra[4 * i + 1], bgra[4 * i + 2], bgra[4 * i + 3]) = (p.B, p.G, p.R, p.A);
        }

        fixed (byte* pixels = bgra)
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, (nint)pixels,
                new PixelSize(image.Width, image.Height), new Vector(96, 96), image.Width * 4);
        }
    }

    // Opaque BGRA from GDI, which has no alpha.
    private static unsafe PixelImage FromGdiBitmap(Bitmap bitmap)
    {
        using (bitmap)
        {
            var (width, height) = (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            ClipboardCodec.CheckSize(width, height);
            if (bitmap.Format != PixelFormat.Bgra8888)
            {
                throw new ImageOpenException(OpenFailure.Unreadable, $"Unexpected clipboard bitmap format {bitmap.Format}.");
            }

            var bgra = new byte[width * height * 4];
            fixed (byte* buffer = bgra)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), (nint)buffer, bgra.Length, width * 4);
            }

            var pixels = new Rgba[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Rgba(bgra[4 * i + 2], bgra[4 * i + 1], bgra[4 * i], 255);
            }

            return new PixelImage(width, height, pixels);
        }
    }
}
