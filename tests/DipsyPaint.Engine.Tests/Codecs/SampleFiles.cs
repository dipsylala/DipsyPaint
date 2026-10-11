using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>Builds image files in memory: valid exports and hand-made headers for the malicious corpus.</summary>
public static class SampleFiles
{
    /// <summary>An opaque document whose pixels vary smoothly (JPEG-friendly).</summary>
    public static Document Gradient(int width = 32, int height = 24)
    {
        var document = Document.CreateBlank(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                document.ActiveLayer.Pixels.SetPixel(x, y, new Rgba((byte)(x * 255 / width), (byte)(y * 255 / height), 128, 255));
            }
        }

        return document;
    }

    public static byte[] Export(Document document, ImageFormat format)
    {
        using var stream = new MemoryStream();
        ImageCodec.Export(new Compositor(document), stream, format);
        return stream.ToArray();
    }

    public static Document Open(byte[] file, MemoryBudget? budget = null) => ImageCodec.Open(new MemoryStream(file), budget);

    /// <summary>
    /// A PNG with the given header and raw IDAT payload (valid CRCs unless <paramref name="breakCrc"/>),
    /// and a <c>pHYs</c> chunk holding <paramref name="phys"/> if given.
    /// </summary>
    public static byte[] Png(int width, int height, byte[]? idat = null, bool breakCrc = false, byte[]? phys = null)
    {
        using var file = new MemoryStream();
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(file, "IHDR", header, breakCrc);
        if (phys is not null)
        {
            WriteChunk(file, "pHYs", phys);
        }

        if (idat is not null)
        {
            WriteChunk(file, "IDAT", idat);
        }

        WriteChunk(file, "IEND", []);
        return file.ToArray();
    }

    /// <summary>Zlib-compressed bytes, for IDAT payloads.</summary>
    public static byte[] Zlib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>A 24-bit BMP header (file + BITMAPINFOHEADER) with no pixel data.</summary>
    public static byte[] BmpHeader(int width, int height, short bitsPerPixel = 24)
    {
        var file = new byte[54];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2), file.Length);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10), 54); // pixel data offset
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(14), 40); // info header size
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(26), 1); // planes
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(28), bitsPerPixel);
        return file;
    }

    /// <summary>A JPEG holding only SOI, a baseline frame header (SOF0) and EOI.</summary>
    public static byte[] JpegHeader(int width, int height)
    {
        byte[] frame =
        [
            0xFF, 0xD8, // SOI
            0xFF, 0xC0, 0x00, 0x11, 0x08, // SOF0, length 17, 8-bit precision
            (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
            0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, // three components
            0xFF, 0xD9, // EOI
        ];
        return frame;
    }

    /// <summary>
    /// A real JPEG export whose frame header (SOF0) is patched to declare <paramref name="width"/>×<paramref name="height"/>,
    /// cut off just after the scan header, so it identifies but holds almost no image data.
    /// </summary>
    public static byte[] JpegDeclaring(int width, int height)
    {
        var file = Export(Gradient(), ImageFormat.Jpeg);
        var frame = file.AsSpan().IndexOf([(byte)0xFF, (byte)0xC0]);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(frame + 5), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(frame + 7), (ushort)width);
        var scan = file.AsSpan().IndexOf([(byte)0xFF, (byte)0xDA]);
        var scanHeaderEnd = scan + 2 + BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(scan + 2));
        return [.. file.AsSpan(0, scanHeaderEnd), 0x00, 0x00, 0xFF, 0xD9];
    }

    private static void WriteChunk(Stream file, string type, byte[] data, bool breakCrc = false)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        file.Write(length);
        file.Write(typeBytes);
        file.Write(data);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]) ^ (breakCrc ? 1u : 0u));
        file.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
