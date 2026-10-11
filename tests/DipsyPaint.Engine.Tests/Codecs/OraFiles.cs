using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>Builds OpenRaster files by hand, valid or damaged, for the round-trip tests and the malicious corpus.</summary>
public static class OraFiles
{
    /// <summary>A 4×4 opaque PNG.</summary>
    public static byte[] SmallPng(int width = 4, int height = 4) => SampleFiles.Export(SampleFiles.Gradient(width, height), ImageFormat.Png);

    /// <summary>A <c>stack.xml</c> for a <paramref name="width"/>×<paramref name="height"/> image holding <paramref name="stack"/>.</summary>
    public static string Stack(int width, int height, string stack) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><image version="0.0.6" w="{width}" h="{height}"><stack>{stack}</stack></image>""";

    /// <summary>A zip holding a correct <c>mimetype</c>, then <paramref name="entries"/> in order.</summary>
    public static byte[] Ora(params (string Name, byte[] Data)[] entries) =>
        Zip([("mimetype", "image/openraster"u8.ToArray()), .. entries]);

    /// <summary>An ORA with <paramref name="stackXml"/> and one 4×4 PNG at <c>data/a.png</c>.</summary>
    public static byte[] Ora(string stackXml) =>
        Ora(("stack.xml", Encoding.UTF8.GetBytes(stackXml)), ("data/a.png", SmallPng()));

    public static byte[] Zip(params (string Name, byte[] Data)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(data);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Patches the uncompressed size the central directory declares for <paramref name="name"/>.</summary>
    public static byte[] DeclaringSize(byte[] zip, string name, uint size)
    {
        var copy = (byte[])zip.Clone();
        for (var i = 0; i + 46 <= copy.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(i)) != 0x02014B50)
            {
                continue;
            }

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(copy.AsSpan(i + 28));
            if (Encoding.UTF8.GetString(copy, i + 46, nameLength) == name)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 24), size);
            }
        }

        return copy;
    }

    public static Document Open(byte[] file, MemoryBudget? budget = null) => ImageCodec.Open(new MemoryStream(file), budget);

    public static byte[] Save(Compositor compositor)
    {
        using var stream = new MemoryStream();
        ImageCodec.Export(compositor, stream, ImageFormat.OpenRaster);
        return stream.ToArray();
    }
}
