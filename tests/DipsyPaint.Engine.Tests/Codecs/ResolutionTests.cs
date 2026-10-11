using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using DipsyPaint.Engine.Codecs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using static DipsyPaint.Engine.Tests.Codecs.SampleFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>Resolution (DPI): stored by documents, read and written by files (ADR-0024).</summary>
public class ResolutionTests
{
    private static readonly Resolution Print = new(300, 150);

    [Fact]
    public void NewDocuments_Are96By96Dpi()
    {
        var document = Document.CreateBlank();

        Assert.Equal(new Resolution(96, 96, ResolutionUnit.PixelsPerInch), document.Resolution);
        Assert.False(document.ResolutionWasInvalid);
    }

    [Fact]
    public void CreateBlank_TakesAResolution_AndRefusesAnInvalidOne()
    {
        Assert.Equal(Print, Document.CreateBlank(8, 8, Print).Resolution);
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.CreateBlank(8, 8, new Resolution(0, 96)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.CreateBlank(8, 8, new Resolution(96, 10_001)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.CreateBlank(8, 8, new Resolution(double.NaN, 96)));
    }

    [Theory]
    [InlineData(1, 1, ResolutionUnit.PixelsPerInch, true)]
    [InlineData(10_000, 10_000, ResolutionUnit.PixelsPerInch, true)]
    [InlineData(0.99, 96, ResolutionUnit.PixelsPerInch, false)]
    [InlineData(96, 10_000.5, ResolutionUnit.PixelsPerInch, false)]
    [InlineData(3937, 3937, ResolutionUnit.PixelsPerCentimetre, true)] // 9999.98 dpi
    [InlineData(3938, 3938, ResolutionUnit.PixelsPerCentimetre, false)]
    [InlineData(40, 40, ResolutionUnit.PixelsPerMetre, true)] // 1.016 dpi
    [InlineData(39, 39, ResolutionUnit.PixelsPerMetre, false)]
    [InlineData(-300, 300, ResolutionUnit.PixelsPerInch, false)]
    [InlineData(double.PositiveInfinity, 300, ResolutionUnit.PixelsPerInch, false)]
    public void IsValid_AcceptsOneTo10000DpiPerDirection(double horizontal, double vertical, ResolutionUnit unit, bool valid)
    {
        Assert.Equal(valid, new Resolution(horizontal, vertical, unit).IsValid);
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Bmp)]
    [InlineData(ImageFormat.Bmp256)]
    [InlineData(ImageFormat.Bmp16)]
    [InlineData(ImageFormat.BmpMonochrome)]
    [InlineData(ImageFormat.Tiff)]
    [InlineData(ImageFormat.OpenRaster)]
    public void Export_WritesTheResolution_AndOpenReadsItBack(ImageFormat format)
    {
        var reopened = Open(Export(Document.CreateBlank(8, 8, Print), format));

        Assert.Equal(300, reopened.Resolution.HorizontalDpi, 0.05);
        Assert.Equal(150, reopened.Resolution.VerticalDpi, 0.05);
        Assert.False(reopened.ResolutionWasInvalid);
    }

    [Fact]
    public void Gif_IsSavedWithoutAResolution_AndOpensAs96Dpi()
    {
        var reopened = Open(Export(Document.CreateBlank(8, 8, Print), ImageFormat.Gif));

        Assert.Equal(Resolution.Default, reopened.Resolution);
        Assert.False(reopened.ResolutionWasInvalid);
    }

    public static TheoryData<string> StoredSamples => [.. Stored.Keys];

    // Files as other programs write them, in each format's own units.
    private static readonly Dictionary<string, (Func<byte[]> File, ImageFormat Format, double HorizontalDpi, double VerticalDpi)> Stored = new()
    {
        ["png-pixels-per-metre"] = (() => Encode(new PngEncoder(), 11811, 7874, PixelResolutionUnit.PixelsPerMeter), ImageFormat.Png, 300, 200),
        ["jpeg-dpi"] = (() => Encode(new JpegEncoder(), 300, 200, PixelResolutionUnit.PixelsPerInch), ImageFormat.Jpeg, 300, 200),
        ["jpeg-dots-per-cm"] = (() => Encode(new JpegEncoder(), 118, 47, PixelResolutionUnit.PixelsPerCentimeter), ImageFormat.Jpeg, 299.72, 119.38),
        ["bmp-pixels-per-metre"] = (() => Encode(new BmpEncoder(), 11811, 3780, PixelResolutionUnit.PixelsPerMeter), ImageFormat.Bmp, 300, 96),
        ["tiff-inch"] = (() => Encode(new TiffEncoder(), 300, 150, PixelResolutionUnit.PixelsPerInch), ImageFormat.Tiff, 300, 150),
        ["tiff-fractional-inch"] = (() => Encode(new TiffEncoder(), 300.5, 72.25, PixelResolutionUnit.PixelsPerInch), ImageFormat.Tiff, 300.5, 72.25),
        ["tiff-centimetre"] = (() => Encode(new TiffEncoder(), 118.5, 59, PixelResolutionUnit.PixelsPerCentimeter), ImageFormat.Tiff, 300.99, 149.86),
    };

    [Theory]
    [MemberData(nameof(StoredSamples))]
    public void OpeningAndResaving_WritesTheStoredValuesBackExactly(string name)
    {
        var (file, format, horizontalDpi, verticalDpi) = Stored[name];
        var original = file();

        var document = Open(original);
        var resaved = Export(document, format);

        Assert.Equal(horizontalDpi, document.Resolution.HorizontalDpi, 0.05);
        Assert.Equal(verticalDpi, document.Resolution.VerticalDpi, 0.05);
        var (before, after) = (StoredResolution(original), StoredResolution(resaved));
        Assert.Equal(before, after);
    }

    [Fact]
    public void OpenRaster_ReadsAndWritesXresAndYres()
    {
        var file = OraFiles.Ora("""<image version="0.0.6" w="4" h="4" xres="300" yres="150"><stack><layer src="data/a.png"/></stack></image>""");

        var document = Open(file);
        var stack = StackXml(OraFiles.Save(new Compositor(document)));

        Assert.Equal(Print, document.Resolution);
        Assert.Contains("xres=\"300\" yres=\"150\"", stack, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenRaster_WritesWholeDpi()
    {
        var document = Open(Encode(new PngEncoder(), 11811, 3780, PixelResolutionUnit.PixelsPerMeter)); // 299.9994 × 96.012 dpi

        var stack = StackXml(OraFiles.Save(new Compositor(document)));

        Assert.Contains("xres=\"300\" yres=\"96\"", stack, StringComparison.Ordinal);
    }

    [Fact]
    public void CrashRecoverySnapshots_KeepTheResolution()
    {
        using var stream = new MemoryStream();
        OpenRasterSnapshot.Capture(new Compositor(Document.CreateBlank(8, 8, Print))).WriteTo(stream);

        Assert.Equal(Print, Open(stream.ToArray()).Resolution);
    }

    [Fact]
    public void Jpeg_WithOnlyAnExifResolution_IsRead()
    {
        var file = WithoutJfif(Encode(new JpegEncoder(), 300, 200, PixelResolutionUnit.PixelsPerInch, Exif(300, 200)));

        var document = Open(file);

        Assert.Equal(new Resolution(300, 200), document.Resolution);
        Assert.False(document.ResolutionWasInvalid);
    }

    [Fact]
    public void Jpeg_WithAnAspectRatioOnlyJfif_FallsBackToExif()
    {
        var file = Encode(new JpegEncoder(), 300, 200, PixelResolutionUnit.PixelsPerInch, Exif(300, 200));
        file[13] = 0; // JFIF units: none

        Assert.Equal(new Resolution(300, 200), Open(file).Resolution);
    }

    public static TheoryData<string> WithoutResolutionNames => [.. WithoutResolution.Keys];

    // No resolution stated, or only an aspect ratio: 96 dpi, and nothing to report.
    private static readonly Dictionary<string, Func<byte[]>> WithoutResolution = new()
    {
        ["png-no-phys"] = () => Png(4, 4, Zlib(new byte[4 * 17])),
        ["png-aspect-ratio-only"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(2, 1, unit: 0)),
        ["bmp-zero"] = () => PatchBmp(0, 0),
        ["jpeg-aspect-ratio-only"] = () => PatchJfif(unit: 0, 1, 1),
        ["tiff-no-resolution"] = () => FormatSamples.TiffHeader(4, 4, new byte[16]),
        ["tiff-aspect-ratio-only"] = () => PatchTiff(unit: 1),
        ["ora-no-xres"] = () => OraFiles.Ora(OraFiles.Stack(4, 4, """<layer src="data/a.png"/>""")),
    };

    [Theory]
    [MemberData(nameof(WithoutResolutionNames))]
    public void FileWithoutResolution_Opens96DpiAndValid(string name)
    {
        var document = Open(WithoutResolution[name]());

        Assert.Equal(Resolution.Default, document.Resolution);
        Assert.False(document.ResolutionWasInvalid);
    }

    public static TheoryData<string> MaliciousNames => [.. Malicious.Keys];

    /// <summary>
    /// Malicious-sample corpus for resolution values (ADR-0014, ADR-0024). Each must open, quickly, at
    /// 96 dpi with <see cref="Document.ResolutionWasInvalid"/> set.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> Malicious = new()
    {
        ["png-zero"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(0, 0)),
        ["png-zero-one-direction"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(11811, 0)),
        ["png-huge"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(uint.MaxValue, uint.MaxValue)),
        ["png-too-small"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(1, 1)),
        ["png-unknown-unit"] = () => Png(4, 4, Zlib(new byte[4 * 17]), phys: Phys(11811, 11811, unit: 7)),
        ["bmp-negative"] = () => PatchBmp(-11811, -11811),
        ["bmp-negative-one-direction"] = () => PatchBmp(11811, -1),
        ["bmp-huge"] = () => PatchBmp(int.MaxValue, int.MaxValue),
        ["jpeg-zero"] = () => PatchJfif(unit: 1, 0, 0),
        ["jpeg-zero-dots-per-cm"] = () => PatchJfif(unit: 2, 0, 0),
        ["jpeg-huge"] = () => PatchJfif(unit: 1, 65535, 65535),
        ["jpeg-unknown-unit"] = () => PatchJfif(unit: 9, 300, 300),
        ["tiff-zero"] = () => PatchTiff(xNumerator: 0),
        ["tiff-zero-denominator"] = () => PatchTiff(xDenominator: 0),
        ["tiff-zero-over-zero"] = () => PatchTiff(xNumerator: 0, xDenominator: 0),
        ["tiff-huge"] = () => PatchTiff(xNumerator: uint.MaxValue),
        ["tiff-too-small"] = () => PatchTiff(xNumerator: 1, xDenominator: 2),
        ["tiff-unknown-unit"] = () => PatchTiff(unit: 9),
        ["ora-non-numeric"] = () => OraWithResolution("""xres="three hundred" yres="300" """),
        ["ora-empty"] = () => OraWithResolution("""xres="" yres="" """),
        ["ora-nan"] = () => OraWithResolution("""xres="NaN" yres="NaN" """),
        ["ora-infinity"] = () => OraWithResolution("""xres="Infinity" yres="300" """),
        ["ora-zero"] = () => OraWithResolution("""xres="0" yres="0" """),
        ["ora-negative"] = () => OraWithResolution("""xres="-300" yres="300" """),
        ["ora-huge"] = () => OraWithResolution("""xres="1e308" yres="300" """),
        ["ora-xres-only"] = () => OraWithResolution("""xres="300" """),
    };

    [Theory]
    [MemberData(nameof(MaliciousNames))]
    public void MaliciousResolution_Opens96DpiAndMarkedInvalid(string name)
    {
        var file = Malicious[name]();
        var clock = Stopwatch.StartNew();

        var document = Open(file);

        Assert.Equal(Resolution.Default, document.Resolution);
        Assert.True(document.ResolutionWasInvalid);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{name} took {clock.Elapsed}");
    }

    [Fact]
    public void ImageOperations_AndUndoingThem_KeepTheResolution()
    {
        var document = Document.CreateBlank(40, 30, Print);
        var editor = new DocumentEditor(document);

        editor.Resize(60, 45);
        editor.Transform(ImageTransform.RotateRight);
        editor.Rotate(30);
        editor.ResizeAndSkew(document.Width, document.Height, 20, -10);
        editor.Select(new IntRect(2, 2, 20, 10));
        editor.CropToSelection();
        editor.Paste(new PixelImage(50, 50, new Rgba[50 * 50]), 10, 10);
        editor.CommitPending();

        Assert.Equal((60, 60), (document.Width, document.Height)); // the paste grew the canvas
        Assert.Equal(Print, document.Resolution);
        while (editor.History.CanUndo)
        {
            editor.Undo();
        }

        Assert.Equal((40, 30), (document.Width, document.Height));
        Assert.Equal(Print, document.Resolution);
        Assert.Equal(300, Open(Export(document, ImageFormat.Png)).Resolution.HorizontalDpi, 0.01);
    }

    private static byte[] Encode(ImageEncoder encoder, double horizontal, double vertical, PixelResolutionUnit unit, ExifProfile? exif = null)
    {
        using var image = new Image<Rgba32>(4, 4, new Rgba32(10, 20, 30, 255));
        image.Metadata.HorizontalResolution = horizontal;
        image.Metadata.VerticalResolution = vertical;
        image.Metadata.ResolutionUnits = unit;
        image.Metadata.ExifProfile = exif;
        using var stream = new MemoryStream();
        image.Save(stream, encoder);
        return stream.ToArray();
    }

    private static ExifProfile Exif(uint horizontal, uint vertical)
    {
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.XResolution, new Rational(horizontal, 1));
        profile.SetValue(ExifTag.YResolution, new Rational(vertical, 1));
        profile.SetValue(ExifTag.ResolutionUnit, (ushort)2); // inch
        return profile;
    }

    /// <summary>The resolution fields as an independent reader (ImageSharp, with metadata) sees them.</summary>
    private static (double, double, PixelResolutionUnit) StoredResolution(byte[] file)
    {
        var metadata = Image.Identify(new DecoderOptions(), new MemoryStream(file)).Metadata;
        return (metadata.HorizontalResolution, metadata.VerticalResolution, metadata.ResolutionUnits);
    }

    private static string StackXml(byte[] ora)
    {
        using var zip = new ZipArchive(new MemoryStream(ora), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("stack.xml")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] Phys(uint x, uint y, byte unit = 1)
    {
        var data = new byte[9];
        BinaryPrimitives.WriteUInt32BigEndian(data, x);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), y);
        data[8] = unit;
        return data;
    }

    private static byte[] PatchBmp(int x, int y)
    {
        var file = Export(Gradient(4, 4), ImageFormat.Bmp);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(38), x);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(42), y);
        return file;
    }

    /// <summary>A JPEG whose JFIF segment (which ImageSharp writes first) holds these values.</summary>
    private static byte[] PatchJfif(byte unit, ushort x, ushort y)
    {
        var file = Export(Gradient(4, 4), ImageFormat.Jpeg);
        Assert.True(file.AsSpan(6, 5).SequenceEqual("JFIF\0"u8));
        file[13] = unit;
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(14), x);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(16), y);
        return file;
    }

    private static byte[] WithoutJfif(byte[] jpeg)
    {
        Assert.True(jpeg.AsSpan(6, 5).SequenceEqual("JFIF\0"u8));
        var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(4));
        return [.. jpeg.AsSpan(0, 2), .. jpeg.AsSpan(4 + length)];
    }

    /// <summary>A 300 dpi TIFF with its XResolution or ResolutionUnit tag changed.</summary>
    private static byte[] PatchTiff(uint? xNumerator = null, uint? xDenominator = null, ushort? unit = null)
    {
        var file = Encode(new TiffEncoder(), 300, 300, PixelResolutionUnit.PixelsPerInch);
        var xResolution = (int)BinaryPrimitives.ReadUInt32LittleEndian(TiffEntry(file, 282)[8..]);
        if (xNumerator is { } numerator)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(xResolution), numerator);
        }

        if (xDenominator is { } denominator)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(xResolution + 4), denominator);
        }

        if (unit is { } value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(TiffEntry(file, 296)[8..], value);
        }

        return file;
    }

    /// <summary>The 12-byte entry for <paramref name="tag"/> in a little-endian TIFF's first IFD.</summary>
    private static Span<byte> TiffEntry(byte[] tiff, ushort tag)
    {
        Assert.True(tiff.AsSpan(0, 2).SequenceEqual("II"u8));
        var ifd = (int)BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(4));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(ifd));
        for (var i = 0; i < count; i++)
        {
            var entry = tiff.AsSpan(ifd + 2 + i * 12, 12);
            if (BinaryPrimitives.ReadUInt16LittleEndian(entry) == tag)
            {
                return entry;
            }
        }

        throw new InvalidOperationException($"The TIFF has no tag {tag}.");
    }

    private static byte[] OraWithResolution(string attributes) =>
        OraFiles.Ora($"""<image version="0.0.6" w="4" h="4" {attributes}><stack><layer src="data/a.png"/></stack></image>""");
}
