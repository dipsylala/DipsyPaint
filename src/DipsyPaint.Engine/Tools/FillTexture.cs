using System.Collections.Concurrent;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tools;

/// <summary>
/// The texture of a textured shape fill (see <see cref="TexturedShapes"/>): how much paint it leaves
/// on each pixel, as the <see cref="Rgba"/> scale (0–255) that <see cref="Rgba.WithOpacity"/> gives
/// the coverage. Values are computed once, in 64×64 tiles as they are first needed, and kept, so a
/// pending shape that is redrawn on every pointer move while it is dragged (issue #102) computes its
/// noise only for pixels it has not covered before. The values are those the texture gives, however
/// they are cached.
/// <para>
/// A crayon, marker or natural pencil texture lies on the paper, in document pixels. An oil or
/// watercolour one is seeded from the shape and lies in the shape's frame: pixel (0, 0) is the
/// frame's top-left, wherever the frame is.
/// </para>
/// </summary>
internal sealed class FillTexture
{
    /// <summary>The tip diameter whose texture a textured oil or watercolour fill spreads, in pixels.</summary>
    public const double TipSize = 20;

    private const int TileShift = 6;
    private const int TileSize = 1 << TileShift;

    private readonly Func<int, int, float> _coverage;
    private readonly ConcurrentDictionary<(int X, int Y), Lazy<byte[]>> _tiles = new();

    private FillTexture(ShapeStyle style, ulong seed, Func<int, int, float> coverage, bool onPaper)
    {
        (Style, Seed, _coverage, OnPaper) = (style, seed, coverage, onPaper);
    }

    public ShapeStyle Style { get; }

    public ulong Seed { get; }

    /// <summary>True when the texture lies on the paper (document pixels), false when it lies in the shape's frame.</summary>
    public bool OnPaper { get; }

    /// <summary>
    /// The texture of a fill in a textured <paramref name="style"/> seeded with <paramref name="seed"/>:
    /// <paramref name="reuse"/> if it is that texture, else a new one.
    /// </summary>
    public static FillTexture For(ShapeStyle style, ulong seed, FillTexture? reuse)
    {
        if (reuse is not null && reuse.Style == style && reuse.Seed == seed)
        {
            return reuse;
        }

        if (BrushTipOf(style) is { } brush)
        {
            return new FillTexture(style, seed, (x, y) => Math.Clamp(brush.Interior(x, y), 0f, 1f), onPaper: true);
        }

        // As TexturedPath.Area spreads it: dragged across the area from left to right, in rows.
        var tipSeed = ArtisticNoise.Hash(seed);
        TexturedTip tip = style switch
        {
            ShapeStyle.Oil => new OilTip(TipSize / 2, tipSeed),
            ShapeStyle.Watercolour => new WatercolourTip(TipSize / 2, tipSeed),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a textured style."),
        };
        return new FillTexture(style, seed, (x, y) => Math.Clamp((float)tip.Texture(y + 0.5, x + 0.5, double.PositiveInfinity), 0f, 1f), onPaper: false);
    }

    /// <summary>
    /// Paints the pixels <paramref name="x"/>.. of row <paramref name="y"/>, in the texture's
    /// coordinates, into <paramref name="destination"/> (as many as it holds) where
    /// <paramref name="needed"/> is true: each gets <paramref name="colours"/>[scale], where
    /// <paramref name="colours"/> holds a colour at each of the 256 scales. Other pixels are left
    /// as they are. Safe to call from several threads at once.
    /// </summary>
    public void Paint(int x, int y, ReadOnlySpan<bool> needed, ReadOnlySpan<Rgba> colours, Span<Rgba> destination)
    {
        needed = needed[..destination.Length];
        var tileY = y >> TileShift;
        var rowInTile = (y - (tileY << TileShift)) << TileShift;
        for (var start = needed.IndexOf(true); start >= 0;)
        {
            // A run of needed pixels, painted a tile at a time.
            var length = needed[start..].IndexOf(false);
            var end = length < 0 ? needed.Length : start + length;
            for (var i = start; i < end;)
            {
                var u = x + i;
                var tileX = u >> TileShift;
                var column = u - (tileX << TileShift);
                var values = Tile(tileX, tileY).AsSpan(rowInTile + column, Math.Min(end - i, TileSize - column));
                var target = destination.Slice(i, values.Length);
                for (var k = 0; k < values.Length; k++)
                {
                    target[k] = colours[values[k]];
                }

                i += values.Length;
            }

            var next = end < needed.Length ? needed[end..].IndexOf(true) : -1;
            start = next < 0 ? -1 : end + next;
        }
    }

    private byte[] Tile(int tileX, int tileY)
    {
        var key = (tileX, tileY);
        if (!_tiles.TryGetValue(key, out var tile))
        {
            tile = _tiles.GetOrAdd(key, new Lazy<byte[]>(() => Compute(tileX, tileY)));
        }

        return tile.Value;
    }

    private byte[] Compute(int tileX, int tileY)
    {
        var tile = new byte[TileSize * TileSize];
        int left = tileX << TileShift, top = tileY << TileShift;
        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                tile[(y << TileShift) + x] = Rgba.ScaleOf(_coverage(left + x, top + y));
            }
        }

        return tile;
    }

    private static BrushTip? BrushTipOf(ShapeStyle style) => style switch
    {
        ShapeStyle.Crayon => BrushTip.Crayon,
        ShapeStyle.Marker => BrushTip.Marker,
        ShapeStyle.NaturalPencil => BrushTip.NaturalPencil,
        _ => null,
    };
}
