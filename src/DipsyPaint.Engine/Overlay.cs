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

    // Content shown by ShowTurned instead of _pixels, and the colour it leaves out; null otherwise.
    private TurnedPixels? _turned;
    private Rgba? _hidden;

    /// <summary>Where the pixels sit, in document pixels; empty when nothing is shown.</summary>
    public IntRect Bounds { get; private set; }

    public bool IsEmpty => Bounds.IsEmpty;

    /// <summary>
    /// Shows <paramref name="pixels"/> (row-major, premultiplied, <paramref name="bounds"/>-sized; any further pixels are ignored)
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
        (_turned, _hidden) = (null, null);
        return dirty;
    }

    /// <summary>
    /// Shows a <paramref name="width"/>×<paramref name="height"/> bitmap (row-major, premultiplied) turned by
    /// <paramref name="transform"/> into <paramref name="bounds"/>, the upright rectangle around it, in place of
    /// the current content: a quick, nearest-neighbour preview of what <see cref="LinearTransform"/>'s bilinear
    /// warp gives, sampled only where it is composited, for showing a turn while it is dragged. Pixels equal to
    /// <paramref name="hidden"/> are left out. The overlay keeps <paramref name="source"/> without copying.
    /// Returns the rectangle to recomposite: the old and new bounds.
    /// </summary>
    internal IntRect ShowTurned(IntRect bounds, Rgba[] source, int width, int height, LinearTransform transform, Rgba? hidden)
    {
        var dirty = Bounds.Union(bounds);
        (Bounds, _pixels) = (bounds, []);
        _turned = new TurnedPixels(source, width, height, transform, bounds.Width, bounds.Height);
        _hidden = hidden;
        return dirty;
    }

    /// <summary>Removes the content; returns the rectangle to recomposite.</summary>
    public IntRect Clear() => Show(IntRect.Empty, []);

    /// <summary>The overlay pixel at a document position; transparent outside <see cref="Bounds"/>.</summary>
    public Rgba GetPixel(int x, int y)
    {
        if (x < Bounds.X || x >= Bounds.Right || y < Bounds.Y || y >= Bounds.Bottom)
        {
            return Rgba.Transparent;
        }

        if (_turned is not null)
        {
            var pixel = Rgba.Transparent;
            CompositeRow(y, x, new Span<Rgba>(ref pixel));
            return pixel;
        }

        return _pixels[(y - Bounds.Y) * Bounds.Width + x - Bounds.X];
    }

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
        if (_turned is not null)
        {
            if (from < to)
            {
                _turned.CompositeRow(y - Bounds.Y, from - Bounds.X, destination[(from - x)..(to - x)], _hidden);
            }

            return;
        }

        if (from < to)
        {
            PixelBlend.Over(_pixels.AsSpan((y - Bounds.Y) * Bounds.Width + from - Bounds.X, to - from), destination[(from - x)..(to - x)]);
        }
    }
}
