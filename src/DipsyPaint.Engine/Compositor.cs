namespace DipsyPaint.Engine;

/// <summary>
/// Holds the composite of a document's layers (source-over, bottom first) and
/// recomposites only the rectangles reported as dirty (ADR-0018). An optional <see cref="Overlay"/>
/// is composited directly above the active layer.
/// </summary>
public sealed class Compositor
{
    private readonly Document _document;
    private readonly Overlay? _overlay;
    private readonly Rgba[] _composite;
    private Rgba[] _row = [];

    public Compositor(Document document, Overlay? overlay = null)
    {
        _document = document;
        _overlay = overlay;
        _composite = new Rgba[document.Width * document.Height];
        Update(new IntRect(0, 0, Width, Height));
    }

    public int Width => _document.Width;
    public int Height => _document.Height;

    /// <summary>The composited document, row-major, premultiplied.</summary>
    public ReadOnlySpan<Rgba> Pixels => _composite;

    /// <summary>Recomposites the part of <paramref name="dirty"/> that lies on the canvas.</summary>
    public void Update(IntRect dirty)
    {
        var region = dirty.Intersect(new IntRect(0, 0, Width, Height));
        if (region.IsEmpty)
        {
            return;
        }

        if (_row.Length < region.Width)
        {
            _row = new Rgba[region.Width];
        }

        var source = _row.AsSpan(0, region.Width);
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var target = _composite.AsSpan(y * Width + region.X, region.Width);
            target.Clear();
            foreach (var layer in _document.Layers)
            {
                layer.Pixels.ReadRegion(new IntRect(region.X, y, region.Width, 1), source);
                for (var x = 0; x < target.Length; x++)
                {
                    target[x] = source[x].Over(target[x]);
                }

                if (layer == _document.ActiveLayer)
                {
                    _overlay?.CompositeRow(y, region.X, target);
                }
            }
        }
    }
}
