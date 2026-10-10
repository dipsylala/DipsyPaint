namespace DipsyPaint.Engine;

/// <summary>A raster plane the size of the canvas, composited into its document.</summary>
public sealed class Layer
{
    internal Layer(int width, int height, Rgba fill) => Pixels = new FlatPixels(width, height, fill);

    /// <summary>The layer's pixels; the only way to read or write them.</summary>
    public IPixelAccess Pixels { get; }
}
