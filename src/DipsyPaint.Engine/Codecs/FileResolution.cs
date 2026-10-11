using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// Reads and writes the resolution of flat image files (ADR-0024). PNG, BMP and JPEG (JFIF) values are
/// read from the file's own fields, because ImageSharp replaces zero and negative values with its
/// default while keeping the file's unit; TIFF and JPEG (EXIF) values come from the EXIF profile.
/// </summary>
internal static class FileResolution
{
    // A stated value that cannot be used; never valid.
    private static readonly Resolution Unreadable = new(double.NaN, double.NaN);

    /// <summary>
    /// The resolution of the <paramref name="format"/> file starting at <paramref name="start"/> in
    /// <paramref name="source"/> (seekable), and whether the file stated one that could not be used.
    /// Missing and aspect-ratio-only values give the default; so does any failure (ADR-0014).
    /// </summary>
    /// <param name="metadataDecoding">Options that read metadata, for the EXIF profile.</param>
    public static (Resolution Resolution, bool Invalid) Read(Stream source, long start, IImageFormat? format, DecoderOptions metadataDecoding)
    {
        Resolution? stated;
        try
        {
            source.Position = start;
            stated = format switch
            {
                PngFormat => ReadPng(source),
                BmpFormat => ReadBmp(source),
                JpegFormat => ReadJfif(source) ?? ReadExif(source, start, metadataDecoding),
                TiffFormat => ReadExif(source, start, metadataDecoding),
                _ => null,
            };
        }
        catch (Exception)
        {
            stated = Unreadable;
        }

        return stated switch
        {
            null => (Resolution.Default, false),
            { IsValid: true } resolution => (resolution, false),
            _ => (Resolution.Default, true),
        };
    }

    /// <summary>Sets <paramref name="metadata"/> so an encoder writes <paramref name="resolution"/>.</summary>
    public static void Write(ImageMetadata metadata, Resolution resolution)
    {
        metadata.HorizontalResolution = resolution.Horizontal;
        metadata.VerticalResolution = resolution.Vertical;
        metadata.ResolutionUnits = resolution.Unit switch
        {
            ResolutionUnit.PixelsPerCentimetre => PixelResolutionUnit.PixelsPerCentimeter,
            ResolutionUnit.PixelsPerMetre => PixelResolutionUnit.PixelsPerMeter,
            _ => PixelResolutionUnit.PixelsPerInch,
        };
    }

    /// <summary>The <c>pHYs</c> chunk, which must come before the image data.</summary>
    private static Resolution? ReadPng(Stream source)
    {
        Span<byte> chunk = stackalloc byte[9];
        source.Position += 8; // signature
        while (source.Length - source.Position >= 8)
        {
            source.ReadExactly(chunk[..8]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunk);
            var type = chunk[4..8];
            if (type.SequenceEqual("pHYs"u8))
            {
                if (length != 9)
                {
                    return Unreadable;
                }

                source.ReadExactly(chunk);
                var (x, y) = (BinaryPrimitives.ReadUInt32BigEndian(chunk), BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]));
                return chunk[8] switch
                {
                    0 => null, // aspect ratio only
                    1 => new Resolution(x, y, ResolutionUnit.PixelsPerMetre),
                    _ => Unreadable,
                };
            }

            if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8))
            {
                return null;
            }

            source.Position += length + 4L; // data and CRC
        }

        return null;
    }

    /// <summary>The info header's pixels per metre; zero in both directions means "not stated" by convention.</summary>
    private static Resolution? ReadBmp(Stream source)
    {
        Span<byte> header = stackalloc byte[46];
        source.ReadExactly(header);
        // OS/2 core headers (12 bytes) have no resolution.
        if (BinaryPrimitives.ReadInt32LittleEndian(header[14..]) < 40)
        {
            return null;
        }

        var (x, y) = (BinaryPrimitives.ReadInt32LittleEndian(header[38..]), BinaryPrimitives.ReadInt32LittleEndian(header[42..]));
        return (x, y) == (0, 0) ? null : new Resolution(x, y, ResolutionUnit.PixelsPerMetre);
    }

    /// <summary>The JFIF segment, which must directly follow the start of image; null if there is none or it gives only an aspect ratio.</summary>
    private static Resolution? ReadJfif(Stream source)
    {
        Span<byte> header = stackalloc byte[18];
        if (source.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
            || header[2] != 0xFF || header[3] != 0xE0 || BinaryPrimitives.ReadUInt16BigEndian(header[4..]) < 16
            || !header[6..11].SequenceEqual("JFIF\0"u8))
        {
            return null;
        }

        var (x, y) = (BinaryPrimitives.ReadUInt16BigEndian(header[14..]), BinaryPrimitives.ReadUInt16BigEndian(header[16..]));
        return header[13] switch
        {
            0 => null,
            1 => new Resolution(x, y),
            2 => new Resolution(x, y, ResolutionUnit.PixelsPerCentimetre),
            _ => Unreadable,
        };
    }

    /// <summary>The EXIF (or TIFF) XResolution, YResolution and ResolutionUnit tags.</summary>
    private static Resolution? ReadExif(Stream source, long start, DecoderOptions metadataDecoding)
    {
        source.Position = start;
        if (Image.Identify(metadataDecoding, source).Metadata.ExifProfile is not { } profile)
        {
            return null;
        }

        var hasX = profile.TryGetValue(ExifTag.XResolution, out var x);
        var hasY = profile.TryGetValue(ExifTag.YResolution, out var y);
        if (!hasX || !hasY)
        {
            return hasX || hasY ? Unreadable : null;
        }

        // A zero denominator gives infinity or NaN, which is never valid.
        var (horizontal, vertical) = (x!.Value.ToDouble(), y!.Value.ToDouble());
        // TIFF's default unit is the inch.
        var unit = profile.TryGetValue(ExifTag.ResolutionUnit, out var u) ? u!.Value : (ushort)2;
        return unit switch
        {
            1 => null, // no absolute unit: aspect ratio only
            2 => new Resolution(horizontal, vertical),
            3 => new Resolution(horizontal, vertical, ResolutionUnit.PixelsPerCentimetre),
            _ => Unreadable,
        };
    }
}
