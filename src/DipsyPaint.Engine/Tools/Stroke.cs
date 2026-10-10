namespace DipsyPaint.Engine.Tools;

/// <summary>
/// One continuous mark on a layer, from pointer down to pointer up. Each plotted pixel is
/// composited source-over in the stroke's colour at the stroke's opacity, at most once per
/// stroke, so a stroke that crosses itself does not build up.
/// </summary>
public sealed class Stroke
{
    private readonly IPixelAccess _target;
    private readonly Rgba _paint;
    private readonly HashSet<(int X, int Y)> _plotted = [];
    private IntRect _dirty;

    public Stroke(IPixelAccess target, Rgba color, float opacity = 1f)
    {
        _target = target;
        _paint = color.WithOpacity(opacity);
    }

    /// <summary>Paints one pixel; pixels off the layer are ignored.</summary>
    public void Plot(int x, int y)
    {
        if ((uint)x >= (uint)_target.Width || (uint)y >= (uint)_target.Height || !_plotted.Add((x, y)))
        {
            return;
        }

        _target.SetPixel(x, y, _paint.Over(_target.GetPixel(x, y)));
        _dirty = _dirty.Union(new IntRect(x, y, 1, 1));
    }

    /// <summary>Paints an aliased 1 px line including both end points (Bresenham).</summary>
    public void Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1, stepY = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            Plot(x0, y0);
            if (x0 == x1 && y0 == y1)
            {
                return;
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

    /// <summary>Returns the rectangle of pixels changed since the last call and resets it.</summary>
    public IntRect TakeDirty()
    {
        var dirty = _dirty;
        _dirty = IntRect.Empty;
        return dirty;
    }
}
