namespace DipsyPaint.Engine;

/// <summary>A raster plane the size of the canvas, composited into its document.</summary>
public sealed class Layer
{
    private readonly FlatPixels _pixels;

    internal Layer(int width, int height, Rgba fill) => _pixels = new FlatPixels(width, height, fill);

    /// <summary>The layer's pixels; the only way to read or write them.</summary>
    public IPixelAccess Pixels => _pixels;

    /// <summary>The layer's storage, for whole-layer operations such as a canvas resize.</summary>
    internal FlatPixels Storage => _pixels;
}
