namespace DipsyPaint.Engine;

/// <summary>
/// Holds the composite of a document's visible layers (source-over, bottom first, each at its
/// opacity) and recomposites only the rectangles reported as dirty (ADR-0018). An optional
/// <see cref="Overlay"/> is composited directly above the active layer, and is hidden with it.
/// </summary>
/// <remarks>
/// The layers below the active layer and the layers above it are each kept composited in a cache
/// (ADR-0018), so a change to the active layer (<see cref="UpdateActive"/>, every stroke) costs
/// three layers' work however many layers there are. The caches are rebuilt whenever the stack
/// changes: a layer added, removed, moved, shown, hidden or given another opacity, or another
/// layer made active. A single-layer document has no caches.
/// </remarks>
public sealed class Compositor
{
    private readonly Document _document;
    private readonly Overlay? _overlay;
    private Rgba[] _composite = [];
    private Rgba[] _row = [];

    // The layers below and above the active layer, composited over transparency; null when there are none.
    private Rgba[]? _below;
    private Rgba[]? _above;

    // The stack the caches were built for.
    private (Layer Layer, bool IsVisible, double Opacity)[] _cachedStack = [];
    private Layer? _cachedActive;

    public Compositor(Document document, Overlay? overlay = null)
    {
        _document = document;
        _overlay = overlay;
        FitDocument();
    }

    /// <summary>The document being composited.</summary>
    internal Document Document => _document;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>The composited document, row-major, premultiplied.</summary>
    public ReadOnlySpan<Rgba> Pixels => _composite;

    /// <summary>
    /// Recomposites the part of <paramref name="dirty"/> that lies on the canvas, where any layer's
    /// pixels may have changed.
    /// </summary>
    public void Update(IntRect dirty)
    {
        var region = dirty.Intersect(new IntRect(0, 0, Width, Height));
        if (region.IsEmpty)
        {
            return;
        }

        if (!PrepareCaches())
        {
            BuildCaches(region);
        }

        Compose(region);
    }

    /// <summary>
    /// Recomposites the part of <paramref name="dirty"/> that lies on the canvas, where only the
    /// active layer's pixels or the overlay changed: the fast path for drawing.
    /// </summary>
    public void UpdateActive(IntRect dirty)
    {
        var region = dirty.Intersect(new IntRect(0, 0, Width, Height));
        if (region.IsEmpty)
        {
            return;
        }

        PrepareCaches();
        Compose(region);
    }

    /// <summary>
    /// After the document's canvas size changed: reallocates the composite at the new size and
    /// recomposites all of it. Returns false, doing nothing, if the size is unchanged.
    /// </summary>
    internal bool FitDocument()
    {
        if (Width == _document.Width && Height == _document.Height)
        {
            return false;
        }

        (Width, Height) = (_document.Width, _document.Height);
        _composite = new Rgba[Width * Height];
        _cachedStack = []; // forces the caches to be rebuilt at the new size
        Update(new IntRect(0, 0, Width, Height));
        return true;
    }

    /// <summary>
    /// Rebuilds the caches if the stack changed since they were built, so the next update is fast
    /// (e.g. right after another layer is made active). Returns true if it rebuilt them.
    /// </summary>
    internal bool PrepareCaches()
    {
        var layers = _document.Layers;
        var active = _document.ActiveLayer;
        if (active == _cachedActive && StackIsCached(layers))
        {
            return false;
        }

        _cachedActive = active;
        _cachedStack = [.. layers.Select(l => (l, l.IsVisible, l.Opacity))];
        var index = IndexOf(active);
        var size = Width * Height;
        _below = index > 0 ? Reuse(_below, size) : null;
        _above = index < layers.Count - 1 ? Reuse(_above, size) : null;
        BuildCaches(new IntRect(0, 0, Width, Height));
        return true;
    }

    private bool StackIsCached(IReadOnlyList<Layer> layers)
    {
        if (_cachedStack.Length != layers.Count)
        {
            return false;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            if (_cachedStack[i] != (layers[i], layers[i].IsVisible, layers[i].Opacity))
            {
                return false;
            }
        }

        return true;
    }

    private static Rgba[] Reuse(Rgba[]? buffer, int size) => buffer?.Length == size ? buffer : new Rgba[size];

    private int IndexOf(Layer layer)
    {
        var layers = _document.Layers;
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i] == layer)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The active layer is not in the document.");
    }

    private Span<Rgba> Row(int width)
    {
        if (_row.Length < width)
        {
            _row = new Rgba[width];
        }

        return _row.AsSpan(0, width);
    }

    private void BuildCaches(IntRect region)
    {
        var index = IndexOf(_document.ActiveLayer);
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var offset = y * Width + region.X;
            if (_below is not null)
            {
                CompositeLayers(0, index, region.X, y, _below.AsSpan(offset, region.Width));
            }

            if (_above is not null)
            {
                CompositeLayers(index + 1, _document.Layers.Count, region.X, y, _above.AsSpan(offset, region.Width));
            }
        }
    }

    // Composites the visible layers from..to (exclusive) of row y, from x, over transparency into target.
    private void CompositeLayers(int from, int to, int x, int y, Span<Rgba> target)
    {
        target.Clear();
        var source = Row(target.Length);
        for (var i = from; i < to; i++)
        {
            var layer = _document.Layers[i];
            if (layer.IsVisible && layer.Opacity > 0)
            {
                layer.Pixels.ReadRegion(new IntRect(x, y, target.Length, 1), source);
                Blend(source, target, layer.Opacity);
            }
        }
    }

    private void Compose(IntRect region)
    {
        var active = _document.ActiveLayer;
        var source = Row(region.Width);
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var offset = y * Width + region.X;
            var target = _composite.AsSpan(offset, region.Width);
            if (_below is not null)
            {
                _below.AsSpan(offset, region.Width).CopyTo(target);
            }
            else
            {
                target.Clear();
            }

            if (active.IsVisible)
            {
                active.Pixels.ReadRegion(new IntRect(region.X, y, region.Width, 1), source);
                if (active.Opacity >= 1)
                {
                    Blend(source, target, 1);
                    _overlay?.CompositeRow(y, region.X, target);
                }
                else
                {
                    // Pending content is shown as it will be once committed: part of the layer, at its opacity.
                    _overlay?.CompositeRow(y, region.X, source);
                    Blend(source, target, active.Opacity);
                }
            }

            if (_above is not null)
            {
                Blend(_above.AsSpan(offset, region.Width), target, 1);
            }
        }
    }

    private static void Blend(ReadOnlySpan<Rgba> source, Span<Rgba> target, double opacity)
    {
        if (opacity >= 1)
        {
            for (var x = 0; x < target.Length; x++)
            {
                target[x] = source[x].Over(target[x]);
            }
        }
        else if (opacity > 0)
        {
            var scale = (float)opacity;
            for (var x = 0; x < target.Length; x++)
            {
                target[x] = source[x].WithOpacity(scale).Over(target[x]);
            }
        }
    }
}
