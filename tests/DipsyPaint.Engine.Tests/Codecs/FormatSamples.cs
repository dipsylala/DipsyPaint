using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>
/// GIF, TIFF, ICO and WebP files built in memory: well-formed files made with ImageSharp's encoders
/// (the engine cannot write ICO or WebP, nor several frames) and hand-made headers for the malicious corpus.
/// </summary>
public static class FormatSamples
{
    /// <summary>An image whose frames are filled with <paramref name="frames"/> colours in turn.</summary>
    public static byte[] Encode(ImageEncoder encoder, int width, int height, params Rgba32[] frames)
    {
        using var image = new Image<Rgba32>(width, height, frames[0]);
        foreach (var colour in frames.Skip(1))
        {
            using var frame = new Image<Rgba32>(width, height, colour);
            image.Frames.AddFrame(frame.Frames.RootFrame);
        }

        using var stream = new MemoryStream();
        image.Save(stream, encoder);
        return stream.ToArray();
    }

    public static byte[] Gif(int width, int height, params Rgba32[] frames) => Encode(new GifEncoder(), width, height, frames);

    public static byte[] Tiff(int width, int height, params Rgba32[] frames) => Encode(new TiffEncoder(), width, height, frames);

    public static byte[] Ico(int width, int height, Rgba32 colour) => Encode(new IcoEncoder(), width, height, colour);

    public static byte[] Webp(int width, int height, Rgba32 colour) =>
        Encode(new WebpEncoder { FileFormat = WebpFileFormatType.Lossless }, width, height, colour);

    /// <summary>
    /// A GIF89a with a logical screen of <paramref name="width"/>×<paramref name="height"/>, a two-colour
    /// global table and <paramref name="frames"/> 1×1 frames.
    /// </summary>
    public static byte[] GifHeader(int width, int height, int frames = 0)
    {
        using var file = new MemoryStream();
        file.Write("GIF89a"u8);
        Span<byte> screen = stackalloc byte[7];
        BinaryPrimitives.WriteUInt16LittleEndian(screen, (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(screen[2..], (ushort)height);
        screen[4] = 0x80; // global colour table of 2 entries
        file.Write(screen);
        file.Write([0, 0, 0, 255, 255, 255]);
        for (var i = 0; i < frames; i++)
        {
            // Image descriptor at (0, 0), 1×1, then LZW minimum code size 2 and one data block.
            file.Write([0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0x02, 0x02, 0x44, 0x01, 0x00]);
        }

        file.WriteByte(0x3B); // trailer
        return file.ToArray();
    }

    /// <summary>A GIF holding one image descriptor of the given size whose data is <paramref name="data"/>.</summary>
    public static byte[] GifFrame(int screenWidth, int screenHeight, int frameWidth, int frameHeight, byte[] data)
    {
        var header = GifHeader(screenWidth, screenHeight);
        using var file = new MemoryStream();
        file.Write(header.AsSpan(0, header.Length - 1));
        Span<byte> descriptor = stackalloc byte[10];
        descriptor[0] = 0x2C;
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor[5..], (ushort)frameWidth);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor[7..], (ushort)frameHeight);
        file.Write(descriptor);
        file.Write(data);
        file.WriteByte(0x3B);
        return file.ToArray();
    }

    /// <summary>
    /// A little-endian, uncompressed, 8-bit greyscale TIFF with one strip. The strip's offset and byte
    /// count default to the <paramref name="pixels"/> that follow the IFD (none if null).
    /// </summary>
    public static byte[] TiffHeader(uint width, uint height, byte[]? pixels = null, uint? stripOffset = null,
        uint? stripByteCount = null, uint nextIfd = 0, uint rowsPerStrip = uint.MaxValue)
    {
        (ushort Tag, ushort Type, uint Value)[] entries =
        [
            (256, 4, width), // ImageWidth
            (257, 4, height), // ImageLength
            (258, 3, 8), // BitsPerSample
            (259, 3, 1), // Compression: none
            (262, 3, 1), // PhotometricInterpretation: black is zero
            (273, 4, 0), // StripOffsets, patched below
            (277, 3, 1), // SamplesPerPixel
            (278, 4, rowsPerStrip), // RowsPerStrip
            (279, 4, 0), // StripByteCounts, patched below
        ];
        const int ifdOffset = 8;
        var dataOffset = (uint)(ifdOffset + 2 + entries.Length * 12 + 4);
        entries[5].Value = stripOffset ?? dataOffset;
        entries[8].Value = stripByteCount ?? (uint)(pixels?.Length ?? 0);

        var file = new byte[dataOffset + (pixels?.Length ?? 0)];
        "II*\0"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), ifdOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(ifdOffset), (ushort)entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = file.AsSpan(ifdOffset + 2 + i * 12);
            BinaryPrimitives.WriteUInt16LittleEndian(entry, entries[i].Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], entries[i].Type);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 1);
            if (entries[i].Type == 3)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], (ushort)entries[i].Value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], entries[i].Value);
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(ifdOffset + 2 + entries.Length * 12), nextIfd);
        pixels?.CopyTo(file, dataOffset);
        return file;
    }

    /// <summary>
    /// An ICO with one directory entry declaring <paramref name="entrySide"/> px (0 means 256) whose
    /// image is <paramref name="image"/>, a PNG or a BMP without its file header.
    /// </summary>
    public static byte[] IcoHolding(byte[] image, byte entrySide = 0, uint? imageOffset = null, ushort entries = 1)
    {
        var file = new byte[6 + 16 + image.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(2), 1); // icon
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(4), entries);
        var entry = file.AsSpan(6);
        entry[0] = entrySide;
        entry[1] = entrySide;
        BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1); // planes
        BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32); // bits per pixel
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)image.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], imageOffset ?? 22);
        image.CopyTo(file, 22);
        return file;
    }

    /// <summary>An extended WebP (VP8X) declaring a <paramref name="width"/>×<paramref name="height"/> canvas and no image.</summary>
    public static byte[] WebpCanvas(int width, int height)
    {
        var file = new byte[12 + 8 + 10];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)(file.Length - 8));
        "WEBPVP8X"u8.CopyTo(file.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 10);
        WriteUInt24(file.AsSpan(24), width - 1);
        WriteUInt24(file.AsSpan(27), height - 1);
        return file;
    }

    /// <summary>A lossless WebP (VP8L) whose header declares <paramref name="width"/>×<paramref name="height"/>, followed by <paramref name="data"/>.</summary>
    public static byte[] WebpLossless(int width, int height, byte[]? data = null, uint? riffSize = null)
    {
        data ??= [];
        var chunk = 5 + data.Length;
        var file = new byte[12 + 8 + chunk + (chunk & 1)];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), riffSize ?? (uint)(file.Length - 8));
        "WEBPVP8L"u8.CopyTo(file.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), (uint)chunk);
        file[20] = 0x2F; // signature
        // 14 bits each of width - 1 and height - 1, then alpha-used and a 3-bit version of 0.
        var bits = (uint)((width - 1) & 0x3FFF) | ((uint)((height - 1) & 0x3FFF) << 14) | (1u << 28);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(21), bits);
        data.CopyTo(file, 25);
        return file;
    }

    private static void WriteUInt24(Span<byte> target, int value)
    {
        target[0] = (byte)value;
        target[1] = (byte)(value >> 8);
        target[2] = (byte)(value >> 16);
    }
}
