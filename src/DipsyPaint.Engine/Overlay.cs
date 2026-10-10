namespace DipsyPaint.Engine;

/// <summary>
/// Pending content: pixels composited directly above the active layer but not part of it and not
/// in history, such as a shape still being adjusted or a moved selection. A tool shows content
/// here between gestures and later commits it to the layer (<see cref="DrawOnto"/>) or discards
/// it (<see cref="Clear"/>). <see cref="Bounds"/> may extend past the canvas; the off-canvas part
/// is kept but not shown.
/// </summary>
public sealed class Overlay
{
    private Rgba[] _pixels = [];

    /// <summary>Where the pixels sit, in document pixels; empty when nothing is shown.</summary>
    public IntRect Bounds { get; private set; }

    public bool IsEmpty => Bounds.IsEmpty;

    /// <summary>
    /// Shows <paramref name="pixels"/> (row-major, premultiplied, <paramref name="bounds"/>-sized)
    /// in place of the current content. The overlay keeps the array without copying, so the
    /// caller must not change it while it is shown; showing the same array at new bounds moves it.
    /// Returns the rectangle to recomposite: the old and new bounds.
    /// </summary>
    public IntRect Show(IntRect bounds, Rgba[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, Math.Max(bounds.Width * bounds.Height, 0), nameof(pixels));
        var dirty = Bounds.Union(bounds);
        Bounds = bounds.IsEmpty ? IntRect.Empty : bounds;
        _pixels = bounds.IsEmpty ? [] : pixels;
        return dirty;
    }

    /// <summary>Removes the content; returns the rectangle to recomposite.</summary>
    public IntRect Clear() => Show(IntRect.Empty, []);

    /// <summary>The overlay pixel at a document position; transparent outside <see cref="Bounds"/>.</summary>
    public Rgba GetPixel(int x, int y) =>
        x >= Bounds.X && x < Bounds.Right && y >= Bounds.Y && y < Bounds.Bottom
            ? _pixels[(y - Bounds.Y) * Bounds.Width + x - Bounds.X]
            : Rgba.Transparent;

    /// <summary>
    /// Composites the content source-over onto <paramref name="target"/> (normally the active
    /// layer, through a history recorder). Does not clear the overlay. Returns the changed rectangle.
    /// </summary>
    public IntRect DrawOnto(IPixelAccess target)
    {
        var region = Bounds.Intersect(new IntRect(0, 0, target.Width, target.Height));
        if (region.IsEmpty)
        {
            return IntRect.Empty;
        }

        var row = new Rgba[region.Width];
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var rowRect = new IntRect(region.X, y, region.Width, 1);
            target.ReadRegion(rowRect, row);
            CompositeRow(y, region.X, row);
            target.WriteRegion(rowRect, row);
        }

        return region;
    }

    /// <summary>Composites the content of row <paramref name="y"/>, from <paramref name="x"/>, over <paramref name="destination"/>.</summary>
    internal void CompositeRow(int y, int x, Span<Rgba> destination)
    {
        if (y < Bounds.Y || y >= Bounds.Bottom)
        {
            return;
        }

        var from = Math.Max(x, Bounds.X);
        var to = Math.Min(x + destination.Length, Bounds.Right);
        var source = _pixels.AsSpan((y - Bounds.Y) * Bounds.Width);
        for (var i = from; i < to; i++)
        {
            destination[i - x] = source[i - Bounds.X].Over(destination[i - x]);
        }
    }
}
