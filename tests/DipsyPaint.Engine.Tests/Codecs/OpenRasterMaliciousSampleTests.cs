using System.Diagnostics;
using System.Text;
using DipsyPaint.Engine.Codecs;
using static DipsyPaint.Engine.Tests.Codecs.OraFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>
/// Malicious-sample corpus for OpenRaster files (ADR-0014, AC19–AC21). Samples are generated here
/// rather than committed as files. Every one must fail with <see cref="ImageOpenException"/>, quickly.
/// </summary>
public sealed class OpenRasterMaliciousSampleTests : IDisposable
{
    private const string Secret = "dipsypaint-xxe-secret";

    // A file an XXE payload tries to read.
    private readonly string _secretFile = Path.Combine(Directory.CreateTempSubdirectory("dipsypaint-ora-").FullName, "secret.txt");

    public OpenRasterMaliciousSampleTests() => File.WriteAllText(_secretFile, Secret);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_secretFile)!, recursive: true);

    private static readonly string OneLayer = Stack(4, 4, """<layer src="data/a.png"/>""");

    private static byte[] WithLayerNamed(string name) =>
        Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng()), (name, SmallPng()));

    private static readonly Dictionary<string, Func<byte[]>> Samples = new()
    {
        // Zip-slip entry names (rejected even though nothing is ever extracted to disk).
        ["zip-slip-parent"] = () => WithLayerNamed("../evil.png"),
        ["zip-slip-nested-parent"] = () => WithLayerNamed("data/../../evil.png"),
        ["zip-slip-backslash-parent"] = () => WithLayerNamed("data\\..\\..\\evil.png"),
        ["zip-slip-absolute"] = () => WithLayerNamed("/etc/evil.png"),
        ["zip-slip-absolute-backslash"] = () => WithLayerNamed("\\evil.png"),
        ["zip-slip-drive-letter"] = () => WithLayerNamed("C:/evil.png"),
        ["zip-slip-drive-relative"] = () => WithLayerNamed("C:evil.png"),
        ["src-zip-slip"] = () => Ora(Stack(4, 4, """<layer src="../data/a.png"/>""")),
        // Archive size limits.
        ["too-many-entries"] = () => Ora([("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng()),
            .. Enumerable.Range(0, OpenRasterCodec.MaxEntries).Select(i => ($"data/x{i}", Array.Empty<byte>()))]),
        ["uncompressed-size-over-cap"] = () => DeclaringSize(Ora(OneLayer), "data/a.png", (uint)OpenRasterCodec.MaxUncompressedBytes + 1),
        ["uncompressed-size-over-cap-in-total"] = () => DeclaringSize(
            Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng()), ("data/b", new byte[16])),
            "data/b", (uint)OpenRasterCodec.MaxUncompressedBytes - 8),
        ["entry-larger-than-declared"] = () => DeclaringSize(Ora(OneLayer), "data/a.png", 16),
        ["zip-bomb-entry"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", new byte[64 << 20])),
        // stack.xml
        ["stack-xml-oversized"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer + new string(' ', OpenRasterCodec.MaxStackXmlBytes))), ("data/a.png", SmallPng())),
        ["stack-xml-oversized-declared-small"] = () => DeclaringSize(
            Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer + new string(' ', OpenRasterCodec.MaxStackXmlBytes))), ("data/a.png", SmallPng())),
            "stack.xml", 100),
        ["xxe-external-dtd"] = () => Ora("""<?xml version="1.0"?><!DOCTYPE image SYSTEM "http://127.0.0.1:1/evil.dtd"><image version="0.0.6" w="4" h="4"><stack><layer src="data/a.png"/></stack></image>"""),
        ["xml-billion-laughs"] = () => Ora("""
            <?xml version="1.0"?>
            <!DOCTYPE image [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"><!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
            <!ENTITY d "&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;"><!ENTITY e "&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;"><!ENTITY f "&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;">]>
            <image version="0.0.6" w="4" h="4"><stack><layer name="&f;" src="data/a.png"/></stack></image>
            """),
        ["stack-xml-not-xml"] = () => Ora("not xml at all <"),
        ["stack-xml-wrong-root"] = () => Ora("""<svg w="4" h="4"><stack><layer src="data/a.png"/></stack></svg>"""),
        ["stack-xml-no-stack"] = () => Ora("""<image version="0.0.6" w="4" h="4"/>"""),
        ["stack-xml-no-layers"] = () => Ora(Stack(4, 4, "")),
        ["stack-xml-too-many-layers"] = () => Ora(Stack(4, 4, string.Concat(Enumerable.Repeat("""<layer src="data/a.png"/>""", OpenRasterCodec.MaxLayers + 1)))),
        ["stack-xml-no-size"] = () => Ora("""<image version="0.0.6"><stack><layer src="data/a.png"/></stack></image>"""),
        ["stack-xml-bad-size"] = () => Ora("""<image version="0.0.6" w="four" h="4"><stack><layer src="data/a.png"/></stack></image>"""),
        ["missing-stack-xml"] = () => Ora(("data/a.png", SmallPng())),
        // Canvas size (ADR-0014 decompression bombs, applied to the declared canvas).
        ["canvas-zero"] = () => Ora(Stack(0, 0, """<layer src="data/a.png"/>""")),
        ["canvas-negative"] = () => Ora(Stack(-4, 4, """<layer src="data/a.png"/>""")),
        ["canvas-oversized"] = () => Ora(Stack(Document.MaxSide + 1, 4, """<layer src="data/a.png"/>""")),
        ["canvas-over-memory-budget"] = () => Ora(Stack(Document.MaxSide, Document.MaxSide, """<layer src="data/a.png"/><layer src="data/a.png"/>""")),
        // Layers
        ["layer-larger-than-canvas"] = () => Ora(Stack(2, 2, """<layer src="data/a.png"/>""")),
        ["layer-taller-than-canvas"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(Stack(4, 4, """<layer src="data/b.png"/>"""))), ("data/b.png", SmallPng(4, 5))),
        ["layer-png-bomb-header"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)),
            ("data/a.png", SampleFiles.Png(16000, 16000, SampleFiles.Zlib(new byte[1 << 16])))),
        ["layer-no-src"] = () => Ora(Stack(4, 4, "<layer/>")),
        ["layer-src-missing"] = () => Ora(Stack(4, 4, """<layer src="data/missing.png"/>""")),
        ["layer-not-png"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SampleFiles.Export(SampleFiles.Gradient(4, 4), ImageFormat.Jpeg))),
        ["layer-png-truncated"] = () => Ora(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng()[..40])),
        // The container
        ["no-mimetype"] = () => Zip(("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng())),
        ["wrong-mimetype"] = () => Zip(("mimetype", "application/zip"u8.ToArray()), ("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng())),
        ["mimetype-oversized"] = () => Zip(("mimetype", new byte[1 << 20]), ("stack.xml", Encoding.UTF8.GetBytes(OneLayer)), ("data/a.png", SmallPng())),
        ["empty-zip"] = () => Zip(),
        ["zip-signature-only"] = () => "PK\x03\x04"u8.ToArray(),
        ["zip-truncated-half"] = () => Ora(OneLayer)[..(Ora(OneLayer).Length / 2)],
        ["zip-garbage-after-signature"] = () => [.. "PK\x03\x04"u8, .. Garbage(4096)],
    };

    public static TheoryData<string> SampleNames => [.. Samples.Keys];

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void MaliciousSample_FailsCleanlyAsCouldNotOpen(string name)
    {
        var file = Samples[name]();
        var clock = Stopwatch.StartNew();

        Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{name} took {clock.Elapsed}");
    }

    /// <summary>XXE payloads naming a real file: rejected, and the file's content appears nowhere (AC20).</summary>
    [Theory]
    [InlineData("""<image version="0.0.6" w="&xxe;" h="4"><stack><layer src="data/a.png"/></stack></image>""")]
    [InlineData("""<image version="0.0.6" w="4" h="4"><stack><layer name="&xxe;" src="data/a.png"/></stack></image>""")]
    public void XxeSample_FailsCleanlyWithoutReadingTheFile(string image)
    {
        var file = Ora($"""<?xml version="1.0"?><!DOCTYPE image [<!ENTITY xxe SYSTEM "{new Uri(_secretFile).AbsoluteUri}">]>{image}""");

        var error = Assert.Throws<ImageOpenException>(() => Open(file));

        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ZipSlipNames_AreRejectedButDotsInsideNamesAreNot()
    {
        var document = Open(WithLayerNamed("data/layer..1.png"));

        Assert.Single(document.Layers);
    }

    /// <summary>Deeply nested stacks (groups) open or fail cleanly, without overflowing the call stack.</summary>
    [Fact]
    public void DeeplyNestedStacks_OpenOrFailCleanly()
    {
        const int depth = 50_000;
        var stack = string.Concat(Enumerable.Repeat("<stack>", depth)) + """<layer src="data/a.png"/>""" + string.Concat(Enumerable.Repeat("</stack>", depth));
        var clock = Stopwatch.StartNew();

        try
        {
            Assert.Single(Open(Ora(Stack(4, 4, stack))).Layers);
        }
        catch (ImageOpenException)
        {
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
    }

    private static byte[] Garbage(int length)
    {
        var bytes = new byte[length];
        new Random(1234).NextBytes(bytes);
        return bytes;
    }
}
