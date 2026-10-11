using System.Runtime.CompilerServices;

namespace DipsyPaint.Engine;

/// <summary>
/// A raster plane the size of the canvas, composited into its document. Its properties are the ones
/// OpenRaster's <c>stack.xml</c> can hold for a layer (ADR-0006): name, visibility and opacity, plus
/// its place in <see cref="Document.Layers"/>. They change through <see cref="DocumentEditor"/>, so
/// every change is a history step.
/// </summary>
public sealed class Layer
{
    private readonly FlatPixels _pixels;

    internal Layer(int width, int height, Rgba fill) => _pixels = new FlatPixels(width, height, fill);

    /// <summary>A copy of <paramref name="source"/>: its pixels and properties.</summary>
    internal Layer(Layer source)
    {
        _pixels = new FlatPixels(source._pixels);
        Properties = source.Properties;
    }

    /// <summary>The layer's pixels; the only way to read or write them.</summary>
    public IPixelAccess Pixels => _pixels;

    /// <summary>The layer's name; empty unless one was given (e.g. by a project file). The shell does not show it, as in reference Paint.</summary>
    public string Name => Properties.Name;

    /// <summary>Whether the layer is composited. A hidden layer keeps its pixels and can still be drawn on.</summary>
    public bool IsVisible => Properties.IsVisible;

    /// <summary>
    /// How opaque the layer is composited, 0–1 (OpenRaster's <c>opacity</c>). Reference Paint has no
    /// layer opacity, so the shell does not offer it; layered project files can set it.
    /// </summary>
    public double Opacity => Properties.Opacity;

    /// <summary>The layer's storage, for whole-layer operations such as a canvas resize.</summary>
    internal FlatPixels Storage => _pixels;

    /// <summary>All of the layer's properties at once, for history steps.</summary>
    internal LayerProperties Properties { get; set; } = LayerProperties.Default;

    /// <summary>The memory its pixels take, in bytes.</summary>
    internal long SizeBytes => (long)_pixels.Width * _pixels.Height * Unsafe.SizeOf<Rgba>();
}

/// <summary>A layer's properties other than its pixels and position (see <see cref="Layer"/>).</summary>
internal readonly record struct LayerProperties(string Name, bool IsVisible, double Opacity)
{
    public static LayerProperties Default { get; } = new("", true, 1);
}
