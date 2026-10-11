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

    /// <summary>
    /// The resolution files are saved with (ADR-0024). Set when the document is created or opened;
    /// image operations that change the pixels leave it as it is.
    /// </summary>
    public Resolution Resolution { get; }

    /// <summary>
    /// Whether the file this document was opened from stated a resolution that could not be used
    /// (zero, negative, unreadable or out of range), so <see cref="Resolution"/> is the default instead.
    /// </summary>
    public bool ResolutionWasInvalid { get; internal set; }

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

    private Document(int width, int height, Resolution resolution)
    {
        Width = width;
        Height = height;
        Resolution = resolution;
        _activeLayer = new Layer(width, height, Rgba.White);
        _layers.Add(_activeLayer);
    }

    /// <summary>
    /// Creates a document with a single opaque white layer, as reference Paint does, with
    /// <paramref name="resolution"/> (default <see cref="Resolution.Default"/>, 96 × 96 DPI).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A side is outside 1 to <see cref="MaxSide"/>, or the resolution is not <see cref="Resolution.IsValid"/>.</exception>
    public static Document CreateBlank(int width = DefaultWidth, int height = DefaultHeight, Resolution? resolution = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSide);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSide);
        if (resolution is { IsValid: false })
        {
            throw new ArgumentOutOfRangeException(nameof(resolution), resolution,
                $"Each direction must be from {Resolution.MinDpi} to {Resolution.MaxDpi} DPI.");
        }

        return new Document(width, height, resolution ?? Resolution.Default);
    }

    /// <summary>Adds a transparent layer on top of the stack and makes it the active layer.</summary>
    public Layer AddLayer()
    {
        var layer = new Layer(Width, Height, Rgba.Transparent);
        _layers.Add(layer);
        _activeLayer = layer;
        return layer;
    }

    /// <summary>Inserts <paramref name="layer"/> at <paramref name="index"/> (0 is the bottom).</summary>
    internal void InsertLayer(int index, Layer layer) => _layers.Insert(index, layer);

    /// <summary>Removes <paramref name="layer"/>; the caller makes sure another layer is active.</summary>
    internal void RemoveLayer(Layer layer) => _layers.Remove(layer);

    /// <summary>Moves <paramref name="layer"/> to <paramref name="index"/> (0 is the bottom).</summary>
    internal void MoveLayer(Layer layer, int index)
    {
        _layers.Remove(layer);
        _layers.Insert(index, layer);
    }

    /// <summary>The layer stack as it is now: order, properties and active layer.</summary>
    internal LayerStack SaveStack() => new([.. _layers], [.. _layers.Select(l => l.Properties)], _activeLayer);

    /// <summary>Restores a stack saved by <see cref="SaveStack"/>.</summary>
    internal void RestoreStack(LayerStack stack)
    {
        _layers.Clear();
        _layers.AddRange(stack.Layers);
        for (var i = 0; i < stack.Layers.Length; i++)
        {
            stack.Layers[i].Properties = stack.Properties[i];
        }

        _activeLayer = stack.Active;
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

/// <summary>A document's layers (bottom first), their properties and the active layer, as saved for history.</summary>
internal sealed record LayerStack(Layer[] Layers, LayerProperties[] Properties, Layer Active);
