using System.Runtime.InteropServices;

namespace DipsyPaint.Engine.Tools;

/// <summary>
/// One continuous mark on a layer, from pointer down to pointer up. Tools tell the stroke how
/// much of each pixel their tip covers (0–1); the stroke keeps the highest coverage seen per pixel
/// and composites from the pixel's colour before the stroke, so a stroke that crosses itself does
/// not build up. A painting stroke composites its colour source-over; an erasing stroke removes
/// alpha. Both scale coverage by the stroke's opacity.
/// </summary>
public sealed class Stroke
{
    private readonly IPixelAccess _target;
    private readonly Rgba _color;
    private readonly float _opacity;
    private readonly bool _erase;
    private readonly Dictionary<(int X, int Y), (Rgba Before, float Coverage)> _touched = [];
    private IntRect _dirty;

    /// <summary>Creates a stroke that paints <paramref name="color"/> at <paramref name="opacity"/> (0–1).</summary>
    public Stroke(IPixelAccess target, Rgba color, float opacity = 1f)
        : this(target, color, opacity, erase: false)
    {
    }

    private Stroke(IPixelAccess target, Rgba color, float opacity, bool erase)
    {
        _target = target;
        _color = color;
        _opacity = Math.Clamp(opacity, 0f, 1f);
        _erase = erase;
    }

    /// <summary>Creates a stroke that erases to transparency at <paramref name="opacity"/> (0–1).</summary>
    public static Stroke Erase(IPixelAccess target, float opacity = 1f) => new(target, Rgba.Transparent, opacity, erase: true);

    /// <summary>Width of the target in pixels; pixels from here on are off it.</summary>
    public int Width => _target.Width;

    /// <summary>Height of the target in pixels; pixels from here on are off it.</summary>
    public int Height => _target.Height;

    /// <summary>
    /// Marks <paramref name="coverage"/> (0–1) of one pixel as covered by the stroke. Lower or equal
    /// coverage than the pixel already has in this stroke is ignored; pixels off the layer are ignored.
    /// </summary>
    public void Cover(int x, int y, float coverage)
    {
        if ((uint)x >= (uint)_target.Width || (uint)y >= (uint)_target.Height || coverage <= 0f)
        {
            return;
        }

        coverage = Math.Min(coverage, 1f);
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_touched, (x, y), out var exists);
        if (!exists)
        {
            entry.Before = _target.GetPixel(x, y);
        }
        else if (coverage <= entry.Coverage)
        {
            return;
        }

        entry.Coverage = coverage;
        var alpha = coverage * _opacity;
        _target.SetPixel(x, y, _erase ? entry.Before.WithOpacity(1f - alpha) : _color.WithOpacity(alpha).Over(entry.Before));
        _dirty = _dirty.Union(new IntRect(x, y, 1, 1));
    }

    /// <summary>Fully covers one pixel.</summary>
    public void Plot(int x, int y) => Cover(x, y, 1f);

    /// <summary>Paints an aliased 1 px line including both end points (Bresenham).</summary>
    public void Line(int x0, int y0, int x1, int y1)
    {
        foreach (var (x, y) in LinePoints(x0, y0, x1, y1))
        {
            Plot(x, y);
        }
    }

    /// <summary>Returns the rectangle of pixels changed since the last call and resets it.</summary>
    public IntRect TakeDirty()
    {
        var dirty = _dirty;
        _dirty = IntRect.Empty;
        return dirty;
    }

    /// <summary>The points of an aliased line from (x0, y0) to (x1, y1), both included, in order (Bresenham).</summary>
    public static IEnumerable<(int X, int Y)> LinePoints(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1, stepY = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1)
            {
                yield break;
            }

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += stepY;
            }
        }
    }
}
