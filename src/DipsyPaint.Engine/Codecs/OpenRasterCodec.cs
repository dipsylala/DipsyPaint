using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// Opens and saves OpenRaster (<c>.ora</c>) <i>project files</i>, which keep layers (ADR-0006).
/// Files are untrusted (ADR-0014): the archive, <c>stack.xml</c> and every layer PNG are checked
/// against the limits below before anything large is allocated.
/// </summary>
public static class OpenRasterCodec
{
    /// <summary>Most entries an archive may have.</summary>
    public const int MaxEntries = 4096;

    /// <summary>Most layers a file may have.</summary>
    public const int MaxLayers = 1024;

    /// <summary>Largest total uncompressed size of all entries: 1 GiB.</summary>
    public const long MaxUncompressedBytes = 1L << 30;

    /// <summary>Largest uncompressed <c>stack.xml</c>: 1 MiB.</summary>
    public const int MaxStackXmlBytes = 1 << 20;

    /// <summary>Deepest element nesting allowed in <c>stack.xml</c> (groups within groups).</summary>
    public const int MaxNesting = 100;

    /// <summary>The content of the <c>mimetype</c> entry.</summary>
    public const string MimeType = "image/openraster";

    /// <summary>The XML namespace of DipsyPaint's extensions to <c>stack.xml</c>.</summary>
    public const string ExtensionNamespace = "https://github.com/dipsylala/DipsyPaint/ns/openraster/1";

    internal const string ExtensionPrefix = "dipsypaint";

    /// <summary>On a <c>layer</c>: "true" when the layer had no name, so its written name was made up.</summary>
    internal const string UnnamedAttribute = "unnamed";

    internal const string MimeTypeEntry = "mimetype";
    internal const string StackEntry = "stack.xml";
    internal const string MergedImageEntry = "mergedimage.png";
    internal const string ThumbnailEntry = "Thumbnails/thumbnail.png";

    // Layers are PNG only, so nothing else is ever parsed.
    private static readonly DecoderOptions Decoding = new()
    {
        Configuration = new Configuration(new PngConfigurationModule()),
        MaxFrames = 1,
        SkipMetadata = true,
    };

    private static readonly XNamespace Extension = ExtensionNamespace;

    /// <summary>
    /// Writes the document behind <paramref name="compositor"/> as an <c>.ora</c> file, keeping its
    /// layers; <paramref name="stream"/> must be seekable. For writing on another thread, use
    /// <see cref="OpenRasterSnapshot.Capture"/> instead.
    /// </summary>
    public static void Save(Compositor compositor, Stream stream) => OpenRasterSnapshot.Write(compositor, stream);

    /// <summary>
    /// Reads an <c>.ora</c> file into a new document with its layers' order, names, visibility,
    /// opacity and offsets, and its resolution (<c>xres</c>, <c>yres</c>); the layer marked <c>selected</c>
    /// (else the top one) becomes active.
    /// Nested stacks (groups) are flattened into their layers, which take on the groups' visibility
    /// and opacity. Blend modes other than normal are read as normal.
    /// </summary>
    /// <exception cref="ImageOpenException">The file could not be opened, for any reason.</exception>
    public static Document Open(Stream stream, MemoryBudget? budget = null)
    {
        budget ??= MemoryBudget.Default;
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            CheckArchive(zip);
            if (Encoding.ASCII.GetString(Read(zip, MimeTypeEntry, 64)).Trim() != MimeType)
            {
                throw Unreadable("The archive is not an OpenRaster file.");
            }

            var image = LoadStack(Read(zip, StackEntry, MaxStackXmlBytes)).Root;
            if (image is null || image.Name != "image" || image.Element("stack") is not { } root)
            {
                throw Unreadable("stack.xml has no image stack.");
            }

            var width = ParseInt(image, "w") ?? throw Unreadable("stack.xml declares no width.");
            var height = ParseInt(image, "h") ?? throw Unreadable("stack.xml declares no height.");
            // Top first in the file; bottom first in a document.
            var layers = root.Descendants("layer").Reverse().ToList();
            if (layers.Count is 0 or > MaxLayers)
            {
                throw Unreadable($"The file has {layers.Count} layers; 1 to {MaxLayers} are allowed.");
            }

            ImageCodec.CheckSize(width, height, budget, OpenCostPerPixel(layers.Count));
            var (resolution, invalid) = ResolutionOf(image);
            var document = BuildDocument(zip, width, height, resolution, layers);
            document.ResolutionWasInvalid = invalid;
            return document;
        }
        catch (Exception e) when (e is not ImageOpenException)
        {
            // Any failure, including running out of memory, means "could not open" (ADR-0014).
            throw new ImageOpenException(OpenFailure.Unreadable, "The file is not a readable OpenRaster file.", e);
        }
    }

    /// <summary>
    /// Bytes per pixel needed to open <paramref name="layers"/> layers: the layers, the blank layer a
    /// new document starts with, one decoded PNG, the composite, the shell's bitmap and, with more
    /// than one layer, the compositor's two caches; 4 bytes each.
    /// </summary>
    private static long OpenCostPerPixel(int layers) => 4L * (layers + 1 + 3 + (layers > 1 ? 2 : 0));

    /// <summary>Rejects zip-slip names, too many entries and too much uncompressed data (ADR-0014).</summary>
    private static void CheckArchive(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxEntries)
        {
            throw Unreadable($"The archive has {zip.Entries.Count} entries; at most {MaxEntries} are allowed.");
        }

        long total = 0;
        foreach (var entry in zip.Entries)
        {
            CheckEntryName(entry.FullName);
            total += entry.Length;
            if (entry.Length < 0 || total > MaxUncompressedBytes)
            {
                throw Unreadable($"The archive's entries are larger than {MaxUncompressedBytes} bytes in total.");
            }
        }
    }

    /// <summary>Rejects names that are absolute, have a <c>..</c> segment or have a drive letter (any colon).</summary>
    private static void CheckEntryName(string name)
    {
        if (name.Length == 0 || name[0] is '/' or '\\' || name.Contains(':')
            || name.Split('/', '\\').Contains(".."))
        {
            throw Unreadable($"The archive has an unsafe entry name \"{name}\".");
        }
    }

    /// <summary>
    /// Reads the entry <paramref name="name"/>, which must exist and be at most <paramref name="limit"/>
    /// bytes, checking the data is exactly as long as the archive declares.
    /// </summary>
    private static byte[] Read(ZipArchive zip, string name, long limit)
    {
        CheckEntryName(name);
        var entry = zip.GetEntry(name) ?? throw Unreadable($"The archive has no \"{name}\".");
        if (entry.Length > limit || entry.Length > Array.MaxLength)
        {
            throw Unreadable($"\"{name}\" is larger than {limit} bytes.");
        }

        var data = new byte[entry.Length];
        using var stream = entry.Open();
        stream.ReadExactly(data);
        if (stream.ReadByte() != -1)
        {
            throw Unreadable($"\"{name}\" holds more data than declared.");
        }

        return data;
    }

    /// <summary>Parses <c>stack.xml</c> with DTDs prohibited and no resolver, so XXE and entity bombs fail.</summary>
    private static XDocument LoadStack(byte[] xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxStackXmlBytes,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        // Streams through first: deeply nested stacks would make loading and flattening slow.
        using (var check = XmlReader.Create(new MemoryStream(xml), settings))
        {
            while (check.Read())
            {
                if (check.Depth > MaxNesting)
                {
                    throw Unreadable($"stack.xml is nested more than {MaxNesting} levels deep.");
                }
            }
        }

        using var reader = XmlReader.Create(new MemoryStream(xml), settings);
        return XDocument.Load(reader);
    }

    /// <summary>
    /// The image's <c>xres</c> and <c>yres</c>, in DPI. Neither gives the default; anything else that is
    /// not a valid resolution gives the default and counts as invalid (ADR-0024).
    /// </summary>
    private static (Resolution Resolution, bool Invalid) ResolutionOf(XElement image)
    {
        var (x, y) = (image.Attribute("xres")?.Value, image.Attribute("yres")?.Value);
        if (x is null && y is null)
        {
            return (Resolution.Default, false);
        }

        var resolution = new Resolution(ParseDpi(x), ParseDpi(y));
        return resolution.IsValid ? (resolution, false) : (Resolution.Default, true);
    }

    private static double ParseDpi(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dpi) ? dpi : double.NaN;

    private static Document BuildDocument(ZipArchive zip, int width, int height, Resolution resolution, List<XElement> elements)
    {
        var document = Document.CreateBlank(width, height, resolution);
        var blank = document.ActiveLayer;
        Layer? selected = null;
        foreach (var element in elements)
        {
            var layer = new Layer(width, height, Rgba.Transparent);
            DecodeInto(zip, element, layer);
            layer.Properties = PropertiesOf(element);
            document.InsertLayer(document.Layers.Count, layer);
            if (document.Layers.Count == 2)
            {
                // Let the start-up layer go before the next layer is decoded.
                document.ActiveLayer = layer;
                document.RemoveLayer(blank);
            }

            if (element.Attribute("selected")?.Value is { } value && ParseBool(value))
            {
                selected = layer;
            }
        }

        document.ActiveLayer = selected ?? document.Layers[^1];
        return document;
    }

    /// <summary>Decodes the layer's PNG into <paramref name="layer"/> at its offset, clipped to the canvas.</summary>
    private static void DecodeInto(ZipArchive zip, XElement element, Layer layer)
    {
        var source = element.Attribute("src")?.Value ?? throw Unreadable("A layer has no src.");
        using var data = new MemoryStream(Read(zip, source, MaxUncompressedBytes));
        var info = Image.Identify(Decoding, data);
        if (info.Width > layer.Pixels.Width || info.Height > layer.Pixels.Height)
        {
            throw Unreadable($"Layer \"{source}\" is {info.Width}×{info.Height} px, larger than the canvas.");
        }

        data.Position = 0;
        using var image = Image.Load<Rgba32>(Decoding, data);
        if (image.Width > layer.Pixels.Width || image.Height > layer.Pixels.Height)
        {
            throw Unreadable($"Layer \"{source}\" is larger than the canvas.");
        }

        // Offsets beyond ±MaxSide put the whole layer off the canvas anyway; clamping avoids overflow.
        var x = Math.Clamp(ParseInt(element, "x") ?? 0, -Document.MaxSide, Document.MaxSide);
        var y = Math.Clamp(ParseInt(element, "y") ?? 0, -Document.MaxSide, Document.MaxSide);
        PixelBridge.CopyInto(image, layer.Pixels, x, y);
    }

    /// <summary>The layer's own properties combined with those of the groups (nested stacks) it is in.</summary>
    private static LayerProperties PropertiesOf(XElement element)
    {
        var name = element.Attribute("name")?.Value ?? "";
        if (element.Attribute(Extension + UnnamedAttribute)?.Value is { } unnamed && ParseBool(unnamed))
        {
            name = "";
        }

        var visible = true;
        var opacity = 1.0;
        // The root stack has no properties of its own.
        foreach (var node in element.AncestorsAndSelf().TakeWhile(e => e.Parent?.Name != "image"))
        {
            visible &= node.Attribute("visibility")?.Value != "hidden";
            opacity *= ParseOpacity(node.Attribute("opacity")?.Value);
        }

        return new LayerProperties(name, visible, opacity);
    }

    private static double ParseOpacity(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) && !double.IsNaN(opacity)
            ? Math.Clamp(opacity, 0, 1)
            : 1;

    private static int? ParseInt(XElement element, string attribute) =>
        int.TryParse(element.Attribute(attribute)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    // xsd:boolean
    private static bool ParseBool(string value) => value.Trim() is "true" or "1";

    private static ImageOpenException Unreadable(string message) => new(OpenFailure.Unreadable, message);
}
