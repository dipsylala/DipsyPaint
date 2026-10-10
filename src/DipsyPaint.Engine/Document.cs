namespace DipsyPaint.Engine;

/// <summary>An open image being edited: an ordered stack of layers with a fixed canvas size.</summary>
public sealed class Document
{
    /// <summary>Largest allowed canvas side, in pixels (ADR-0005).</summary>
    public const int MaxSide = 16384;

    // Placeholder until reference Paint's default is confirmed by the parity audit.
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;

    private readonly List<Layer> _layers = [];
    private Layer _activeLayer;

    public int Width { get; }
    public int Height { get; }

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
}
