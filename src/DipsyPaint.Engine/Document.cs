namespace DipsyPaint.Engine;

/// <summary>
/// An open image being edited: an ordered stack of layers sharing one canvas size. The size changes
/// only through the image operations of <see cref="DocumentEditor"/> (crop, resize, rotate), which
/// apply to every layer.
/// </summary>
public sealed class Document
{
    /// <summary>Largest allowed canvas side, in pixels (ADR-0005).</summary>
    public const int MaxSide = 16384;

    // Prefilled in File > New's size dialog (ticket 34).
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;

    private readonly List<Layer> _layers = [];
    private Layer _activeLayer;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>The layers, bottom first.</summary>
    public IReadOnlyList<Layer> Layers => _layers;

    /// <summary>The layer that tools draw on.</summary>
    public Layer ActiveLayer
    {
        get => _activeLayer;
        set => _activeLayer = _layers.Contains(value)
            ? value
            : throw new ArgumentException("The layer does not belong to this document.", nameof(value));
    }

    private Document(int width, int height)
    {
        Width = width;
        Height = height;
        _activeLayer = new Layer(width, height, Rgba.White);
        _layers.Add(_activeLayer);
    }

    /// <summary>Creates a document with a single opaque white layer, as reference Paint does.</summary>
    public static Document CreateBlank(int width = DefaultWidth, int height = DefaultHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSide);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSide);
        return new Document(width, height);
    }

    /// <summary>Adds a transparent layer on top of the stack and makes it the active layer.</summary>
    public Layer AddLayer()
    {
        var layer = new Layer(Width, Height, Rgba.Transparent);
        _layers.Add(layer);
        _activeLayer = layer;
        return layer;
    }

    /// <summary>
    /// Gives every layer new <paramref name="width"/>×<paramref name="height"/> pixels, computed by
    /// <paramref name="pixelsOf"/> from the layer's index and current pixels (which it must not change),
    /// and sets the canvas size. All new pixels are computed before any layer changes. Returns each
    /// layer's old pixels.
    /// </summary>
    internal Rgba[][] ReplacePixels(int width, int height, Func<int, Rgba[], Rgba[]> pixelsOf)
    {
        var replacement = _layers.Select((layer, i) => pixelsOf(i, layer.Storage.Buffer)).ToArray();
        var old = new Rgba[_layers.Count][];
        for (var i = 0; i < _layers.Count; i++)
        {
            old[i] = _layers[i].Storage.Replace(width, height, replacement[i]);
        }

        (Width, Height) = (width, height);
        return old;
    }
}
