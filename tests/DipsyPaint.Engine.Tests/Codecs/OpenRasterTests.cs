using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DipsyPaint.Engine.Codecs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static DipsyPaint.Engine.Tests.Codecs.OraFiles;

namespace DipsyPaint.Engine.Tests.Codecs;

/// <summary>OpenRaster project files: structure (AC16) and round-trip (ticket 18, ADR-0006).</summary>
public class OpenRasterTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba HalfBlue = new(0, 0, 128, 128);

    // The baseline OpenRaster attributes (layer-stack spec 0.0.6) plus the published "selected" extension.
    private static readonly HashSet<string> ImageAttributes = ["version", "w", "h", "xres", "yres"];
    private static readonly HashSet<string> LayerAttributes = ["name", "src", "x", "y", "opacity", "visibility", "composite-op", "selected"];

    /// <summary>
    /// Three layers: an opaque bottom layer, a hidden middle layer with a translucent pixel (active),
    /// and a top layer at 37% opacity.
    /// </summary>
    private static DocumentEditor Layered(int width = 8, int height = 6)
    {
        var editor = new DocumentEditor(SampleFiles.Gradient(width, height));
        Assert.True(editor.AddLayer());
        var middle = editor.Document.ActiveLayer;
        middle.Pixels.SetPixel(1, 2, HalfBlue);
        Assert.True(editor.AddLayer());
        var top = editor.Document.ActiveLayer;
        top.Pixels.SetPixel(3, 4, Red);
        editor.SetLayerOpacity(top, 0.37);
        editor.SetLayerVisible(middle, false);
        editor.SelectLayer(middle);
        return editor;
    }

    private static Rgba[] PixelsOf(Layer layer)
    {
        var pixels = new Rgba[layer.Pixels.Width * layer.Pixels.Height];
        layer.Pixels.ReadRegion(new IntRect(0, 0, layer.Pixels.Width, layer.Pixels.Height), pixels);
        return pixels;
    }

    private static Dictionary<string, byte[]> Entries(byte[] ora)
    {
        using var zip = new ZipArchive(new MemoryStream(ora), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var stream = e.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        });
    }

    private static XDocument StackOf(byte[] ora) => XDocument.Parse(Encoding.UTF8.GetString(Entries(ora)["stack.xml"]));

    [Fact]
    public void Save_WritesMimetypeFirstAndStoredUncompressed()
    {
        var ora = Save(Layered().Compositor);

        // Local file header: signature, method 0 (stored) at 8, name length at 26, extra length at 28.
        Assert.Equal(0x04034B50u, BinaryPrimitives.ReadUInt32LittleEndian(ora));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ora.AsSpan(8)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ora.AsSpan(6)) & 0x8); // no data descriptor
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(ora.AsSpan(26)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ora.AsSpan(28)));
        Assert.Equal("mimetypeimage/openraster", Encoding.ASCII.GetString(ora, 30, 24));
        using var zip = new ZipArchive(new MemoryStream(ora), ZipArchiveMode.Read);
        Assert.Equal("mimetype", zip.Entries[0].FullName);
        Assert.Equal(16, zip.Entries[0].CompressedLength);
    }

    [Fact]
    public void Save_WritesStackLayerPngsMergedImageAndThumbnail()
    {
        var editor = Layered();
        var ora = Save(editor.Compositor);
        var entries = Entries(ora);

        Assert.Equal(
            ["mimetype", "stack.xml", "data/layer0.png", "data/layer1.png", "data/layer2.png", "Thumbnails/thumbnail.png", "mergedimage.png"],
            entries.Keys);
        foreach (var name in entries.Keys.Where(n => n.EndsWith(".png", StringComparison.Ordinal)))
        {
            Assert.Equal("PNG", Image.DetectFormat(new MemoryStream(entries[name])).Name);
            using var image = Image.Load<Rgba32>(new MemoryStream(entries[name]));
            Assert.Equal((8, 6), (image.Width, image.Height));
        }

        // The merged image is the composite, in straight alpha.
        using var merged = Image.Load<Rgba32>(new MemoryStream(entries["mergedimage.png"]));
        var composite = editor.Compositor.Pixels.ToArray();
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var p = merged[x, y];
                Assert.Equal(composite[y * 8 + x], new Rgba((byte)((p.R * p.A + 127) / 255), (byte)((p.G * p.A + 127) / 255), (byte)((p.B * p.A + 127) / 255), p.A));
            }
        }
    }

    [Theory]
    [InlineData(1000, 500, 256, 128)]
    [InlineData(300, 1200, 64, 256)]
    [InlineData(40, 30, 40, 30)] // never upscaled
    public void Thumbnail_IsAtMost256PxKeepingTheAspectRatio(int width, int height, int thumbWidth, int thumbHeight)
    {
        var ora = Save(new Compositor(Document.CreateBlank(width, height)));

        using var thumbnail = Image.Load<Rgba32>(new MemoryStream(Entries(ora)["Thumbnails/thumbnail.png"]));
        Assert.Equal((thumbWidth, thumbHeight), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void StackXml_IsBaselineOpenRasterWithOnlyNamespacedExtensions()
    {
        var stack = StackOf(Save(Layered().Compositor));

        var image = stack.Root!;
        Assert.Equal("image", image.Name);
        Assert.Equal(("8", "6"), (image.Attribute("w")?.Value, image.Attribute("h")?.Value));
        Assert.NotNull(image.Attribute("version"));
        Assert.All(stack.Descendants(), e => Assert.Contains(e.Name.ToString(), new[] { "image", "stack", "layer" }));
        var layers = image.Element("stack")!.Elements("layer").ToList();
        Assert.Equal(["data/layer2.png", "data/layer1.png", "data/layer0.png"], layers.Select(l => l.Attribute("src")!.Value));
        Assert.Equal(["visible", "hidden", "visible"], layers.Select(l => l.Attribute("visibility")!.Value));
        Assert.Equal(["0.37", "1", "1"], layers.Select(l => l.Attribute("opacity")!.Value));
        Assert.Equal(["Layer 3", "Layer 2", "Layer 1"], layers.Select(l => l.Attribute("name")!.Value));
        Assert.All(layers, l => Assert.Equal("svg:src-over", l.Attribute("composite-op")!.Value));
        Assert.Equal([false, true, false], layers.Select(l => l.Attribute("selected")?.Value == "true"));

        // Anything that is not baseline OpenRaster is in DipsyPaint's namespace.
        foreach (var attribute in stack.Descendants().SelectMany(e => e.Attributes()).Where(a => !a.IsNamespaceDeclaration))
        {
            var allowed = attribute.Parent!.Name == "image" ? ImageAttributes : LayerAttributes;
            Assert.True(
                attribute.Name.Namespace == XNamespace.None ? allowed.Contains(attribute.Name.LocalName) : attribute.Name.NamespaceName == OpenRasterCodec.ExtensionNamespace,
                $"Unexpected attribute {attribute.Name} on {attribute.Parent.Name}");
        }
    }

    [Fact]
    public void Reopening_RestoresLayersExactly()
    {
        var editor = Layered();
        var document = editor.Document;

        var reopened = Open(Save(editor.Compositor));

        Assert.Equal((document.Width, document.Height), (reopened.Width, reopened.Height));
        Assert.Equal(document.Layers.Count, reopened.Layers.Count);
        for (var i = 0; i < document.Layers.Count; i++)
        {
            var (expected, actual) = (document.Layers[i], reopened.Layers[i]);
            Assert.Equal((expected.Name, expected.IsVisible, expected.Opacity), (actual.Name, actual.IsVisible, actual.Opacity));
            Assert.Equal(PixelsOf(expected), PixelsOf(actual));
        }

        Assert.Same(reopened.Layers[1], reopened.ActiveLayer);
        Assert.Equal(editor.Compositor.Pixels.ToArray(), new Compositor(reopened).Pixels.ToArray());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(1.0 / 3)]
    [InlineData(0.999)]
    public void Reopening_RestoresOpacityExactly(double opacity)
    {
        var editor = new DocumentEditor(Document.CreateBlank(2, 2));
        editor.SetLayerOpacity(editor.Document.ActiveLayer, opacity);

        Assert.Equal(opacity, Open(Save(editor.Compositor)).Layers[0].Opacity);
    }

    [Fact]
    public void Reopening_RestoresNamesFromAnotherApp()
    {
        var ora = Ora(Stack(4, 4, """
            <layer name="Ink &amp; paint" src="data/a.png"/>
            <layer name="Background" src="data/a.png"/>
            <layer src="data/a.png"/>
            """));

        var reopened = Open(Save(new Compositor(Open(ora))));

        Assert.Equal(["", "Background", "Ink & paint"], reopened.Layers.Select(l => l.Name));
    }

    [Fact]
    public void Open_ReadsAnotherAppsFile_WithDefaultsOffsetsAndGroups()
    {
        // Like Krita or MyPaint: layers cropped to their content and placed with x/y, and a group.
        var ora = Ora(
            ("stack.xml", Encoding.UTF8.GetBytes(Stack(10, 8, """
                <layer name="Top" src="data/a.png" x="8" y="-2" composite-op="svg:multiply" opacity="0.5"/>
                <stack name="Group" opacity="0.5" visibility="hidden">
                  <layer name="In group" src="data/a.png" opacity="0.5" selected="true"/>
                </stack>
                <layer name="Bottom" src="data/b.png" visibility="visible"/>
                <text>ignored</text>
                """))),
            ("data/a.png", SmallPng()),
            ("data/b.png", SmallPng(10, 8)));

        var document = Open(ora);

        Assert.Equal((10, 8), (document.Width, document.Height));
        Assert.Equal(["Bottom", "In group", "Top"], document.Layers.Select(l => l.Name));
        Assert.Equal([true, false, true], document.Layers.Select(l => l.IsVisible));
        Assert.Equal([1, 0.25, 0.5], document.Layers.Select(l => l.Opacity));
        Assert.Same(document.Layers[1], document.ActiveLayer);

        // The top layer's 4×4 PNG sits at (8, -2), so only its 2×2 corner at (0, 2) lands, at (8, 0).
        var source = SampleFiles.Gradient(4, 4).ActiveLayer.Pixels;
        var top = document.Layers[2].Pixels;
        Assert.Equal(source.GetPixel(0, 2), top.GetPixel(8, 0));
        Assert.Equal(source.GetPixel(1, 3), top.GetPixel(9, 1));
        Assert.Equal(Rgba.Transparent, top.GetPixel(7, 0));
        Assert.Equal(Rgba.Transparent, top.GetPixel(8, 2));
    }

    [Fact]
    public void Open_WithoutASelectedLayer_MakesTheTopLayerActive()
    {
        var document = Open(Ora(Stack(4, 4, """<layer src="data/a.png"/><layer src="data/a.png"/>""")));

        Assert.Same(document.Layers[^1], document.ActiveLayer);
    }

    [Fact]
    public void Open_DoesNotNeedTheMergedImageOrThumbnail()
    {
        var document = Open(Ora(Stack(4, 4, """<layer src="data/a.png"/>""")));

        Assert.Single(document.Layers);
    }

    [Fact]
    public void FormatOf_OraIsOpenRaster_AndSavingItDoesNotWarn()
    {
        Assert.Equal(ImageFormat.OpenRaster, ImageCodec.FormatOf("drawing.ORA"));
        Assert.Equal(ImageFormat.OpenRaster, ImageCodec.ExportFormatOf("drawing.ora"));
        Assert.True(ImageCodec.CanExport(ImageFormat.OpenRaster));
        Assert.False(ImageCodec.UsesPalette(ImageFormat.OpenRaster));
    }

    [Fact]
    public void Capture_IsIndependentOfLaterEdits_AndCanBeWrittenOnAnotherThread()
    {
        var editor = Layered();
        var expected = Save(editor.Compositor);

        var snapshot = OpenRasterSnapshot.Capture(editor.Compositor);
        editor.Document.ActiveLayer.Pixels.SetPixel(0, 0, Red);
        editor.SetLayerOpacity(editor.Document.Layers[0], 0.5);
        editor.AddLayer();
        byte[] written = [];
        var thread = new Thread(() =>
        {
            using var stream = new MemoryStream();
            snapshot.WriteTo(stream);
            written = stream.ToArray();
        });
        thread.Start();
        thread.Join();

        Assert.Equal(Entries(expected).Values, Entries(written).Values);
    }

    [Fact]
    public void WriteTo_RequiresASeekableStream()
    {
        var snapshot = OpenRasterSnapshot.Capture(new Compositor(Document.CreateBlank(2, 2)));

        Assert.Throws<ArgumentException>(() => snapshot.WriteTo(new NonSeekableStream()));
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
