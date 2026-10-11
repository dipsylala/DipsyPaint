using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// A document's layers, their properties and its composite, ready to be written as an OpenRaster
/// <i>project file</i> (ADR-0006). <see cref="Capture"/> copies the pixels, so it must run on the
/// thread that edits the document (the UI thread); <see cref="WriteTo"/> then encodes and zips them
/// and can run on any thread, as crash-recovery snapshots need (ADR-0013).
/// </summary>
public sealed class OpenRasterSnapshot
{
    /// <summary>The largest side of <c>Thumbnails/thumbnail.png</c>, per the OpenRaster spec.</summary>
    public const int ThumbnailMaxSide = 256;

    private const string Version = "0.0.6";

    private static readonly PngEncoder Png = new() { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8 };

    private readonly int _width;
    private readonly int _height;

    // Bottom first, as in Document.Layers.
    private readonly Layer[] _layers;
    private readonly LayerProperties[] _properties;
    private readonly int _active;
    private readonly PixelImage _merged;

    private OpenRasterSnapshot(Compositor compositor, bool copyLayers)
    {
        var document = compositor.Document;
        (_width, _height) = (document.Width, document.Height);
        _layers = [.. document.Layers.Select(l => copyLayers ? new Layer(l) : l)];
        _properties = [.. document.Layers.Select(l => l.Properties)];
        _active = document.Layers.ToList().IndexOf(document.ActiveLayer);
        _merged = new PixelImage(compositor.Width, compositor.Height, compositor.Pixels.ToArray());
    }

    /// <summary>
    /// Copies the pixels and properties of <paramref name="compositor"/>'s document and its current
    /// composite (which includes any pending, uncommitted content). Call on the thread that edits the
    /// document; the snapshot is then independent of it.
    /// </summary>
    public static OpenRasterSnapshot Capture(Compositor compositor) => new(compositor, copyLayers: true);

    /// <summary>
    /// Writes <paramref name="compositor"/>'s document straight to <paramref name="stream"/> without
    /// copying the layers first; the document must not change until this returns.
    /// </summary>
    internal static void Write(Compositor compositor, Stream stream) => new OpenRasterSnapshot(compositor, copyLayers: false).WriteTo(stream);

    /// <summary>
    /// Writes the snapshot as an <c>.ora</c> file: <c>mimetype</c> (first and stored, as the spec
    /// requires), <c>stack.xml</c>, one PNG per layer under <c>data/</c>, <c>Thumbnails/thumbnail.png</c>
    /// and <c>mergedimage.png</c>. Thread-safe; may be called more than once.
    /// </summary>
    /// <param name="stream">
    /// Must be seekable, so every entry's sizes go in its local header (no data descriptors), which
    /// keeps <c>mimetype</c> readable at a fixed offset.
    /// </param>
    public void WriteTo(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("An OpenRaster file must be written to a seekable stream.", nameof(stream));
        }

        var configuration = new Configuration(new PngConfigurationModule());
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        AddEntry(zip, OpenRasterCodec.MimeTypeEntry, CompressionLevel.NoCompression, s => s.Write(Encoding.ASCII.GetBytes(OpenRasterCodec.MimeType)));
        AddEntry(zip, OpenRasterCodec.StackEntry, CompressionLevel.Optimal, WriteStack);
        for (var i = 0; i < _layers.Length; i++)
        {
            // PNG data is already compressed, so it is stored as is.
            AddEntry(zip, LayerSource(i), CompressionLevel.NoCompression, s =>
            {
                using var image = PixelBridge.ToImage(_layers[i].Pixels, configuration);
                image.Save(s, Png);
            });
        }

        using var merged = PixelBridge.ToImage(_merged, configuration);
        AddEntry(zip, OpenRasterCodec.ThumbnailEntry, CompressionLevel.NoCompression, s =>
        {
            var scale = Math.Min(1.0, (double)ThumbnailMaxSide / Math.Max(_width, _height));
            var size = new Size(Math.Max(1, (int)Math.Round(_width * scale)), Math.Max(1, (int)Math.Round(_height * scale)));
            using var thumbnail = merged.Clone(c => c.Resize(size));
            thumbnail.Save(s, Png);
        });
        AddEntry(zip, OpenRasterCodec.MergedImageEntry, CompressionLevel.NoCompression, s => merged.Save(s, Png));
    }

    private static string LayerSource(int index) => $"data/layer{index}.png";

    private static void AddEntry(ZipArchive zip, string name, CompressionLevel compression, Action<Stream> write)
    {
        using var stream = zip.CreateEntry(name, compression).Open();
        write(stream);
    }

    /// <summary>
    /// Writes <c>stack.xml</c>: every layer is a <c>layer</c> in one root <c>stack</c>, topmost first.
    /// The active layer is marked with OpenRaster's <c>selected</c> extension. Unnamed layers get
    /// "Layer N" for other apps, flagged with <c>dipsypaint:unnamed</c> so they reopen unnamed.
    /// </summary>
    private void WriteStack(Stream stream)
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var xml = XmlWriter.Create(stream, settings);
        xml.WriteStartDocument();
        xml.WriteStartElement("image");
        xml.WriteAttributeString("xmlns", OpenRasterCodec.ExtensionPrefix, null, OpenRasterCodec.ExtensionNamespace);
        xml.WriteAttributeString("version", Version);
        xml.WriteAttributeString("w", Invariant(_width));
        xml.WriteAttributeString("h", Invariant(_height));
        xml.WriteStartElement("stack");
        for (var i = _layers.Length - 1; i >= 0; i--)
        {
            var properties = _properties[i];
            xml.WriteStartElement("layer");
            xml.WriteAttributeString("name", properties.Name.Length > 0 ? properties.Name : $"Layer {i + 1}");
            if (properties.Name.Length == 0)
            {
                xml.WriteAttributeString(OpenRasterCodec.ExtensionPrefix, OpenRasterCodec.UnnamedAttribute, OpenRasterCodec.ExtensionNamespace, "true");
            }

            xml.WriteAttributeString("src", LayerSource(i));
            xml.WriteAttributeString("x", "0");
            xml.WriteAttributeString("y", "0");
            // Shortest form that parses back to the same double.
            xml.WriteAttributeString("opacity", properties.Opacity.ToString("R", CultureInfo.InvariantCulture));
            xml.WriteAttributeString("visibility", properties.IsVisible ? "visible" : "hidden");
            xml.WriteAttributeString("composite-op", "svg:src-over");
            if (i == _active)
            {
                xml.WriteAttributeString("selected", "true");
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
