using System.Runtime.InteropServices;

namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// A painting stroke for textured tips, whose paint builds up each time the tip passes over a
/// pixel again, as wet watercolour does. Like <see cref="Stroke"/>, it composites from the pixel's
/// colour before the stroke. A pixel's coverage in one pass is the highest <c>shape</c> coverage
/// the tip gives it (its outline and antialiasing) times one <c>texture</c> value: the one from the
/// sweep that lay most squarely over the pixel (least <c>beyond</c> the ends of its segment), so
/// the many overlapping sweeps of one pass neither build up nor blur the texture, and the texture
/// follows the path. Each cover says how far <c>along</c> the path it is; covering a pixel more than
/// <see cref="NewPassAfter"/> pixels of path after its current pass began starts a new pass, and
/// passes combine like layers of glaze: <c>1 − (1 − a)(1 − b)</c>. With an infinite
/// <see cref="NewPassAfter"/> nothing builds up within the stroke. The stroke's opacity is the
/// ceiling of the combined coverage.
/// </summary>
public sealed class PassStroke
{
    private readonly IPixelAccess _target;
    private readonly Rgba _color;
    private readonly float _opacity;
    private readonly Dictionary<(int X, int Y), Entry> _touched = [];
    private IntRect _dirty;

    /// <summary>Creates a stroke that paints <paramref name="color"/> at <paramref name="opacity"/> (0–1).</summary>
    public PassStroke(IPixelAccess target, Rgba color, float opacity, double newPassAfter)
    {
        _target = target;
        _color = color;
        _opacity = Math.Clamp(opacity, 0f, 1f);
        NewPassAfter = newPassAfter;
    }

    /// <summary>How many pixels of path after a pixel's pass began a new cover of it counts as a new pass.</summary>
    public double NewPassAfter { get; }

    internal IPixelAccess Target => _target;

    /// <summary>
    /// Covers one pixel with the tip <paramref name="along"/> pixels along the path: the tip's
    /// outline covers <paramref name="shape"/> (0–1) of it and its texture leaves
    /// <paramref name="texture"/> (0–1) of the paint there. <paramref name="beyond"/> is how far the
    /// pixel lies beyond the ends of the segment being swept (0 beside it). Pixels off the layer are ignored.
    /// </summary>
    public void Cover(int x, int y, float shape, float texture, double along, float beyond = 0f)
    {
        if ((uint)x >= (uint)_target.Width || (uint)y >= (uint)_target.Height || shape <= 0f)
        {
            return;
        }

        shape = Math.Min(shape, 1f);
        texture = Math.Clamp(texture, 0f, 1f);
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_touched, (x, y), out var exists);
        if (!exists)
        {
            entry.Before = _target.GetPixel(x, y);
            (entry.Texture, entry.Beyond, entry.PassStart) = (texture, beyond, along);
        }
        else if (along - entry.PassStart > NewPassAfter)
        {
            entry.Done = Combine(entry.Done, entry.Shape * entry.Texture);
            (entry.Shape, entry.Texture, entry.Beyond, entry.PassStart) = (0f, texture, beyond, along);
        }
        else if (beyond < entry.Beyond)
        {
            (entry.Texture, entry.Beyond) = (texture, beyond);
        }
        else if (shape <= entry.Shape)
        {
            return;
        }

        entry.Shape = Math.Max(entry.Shape, shape);
        var alpha = Combine(entry.Done, entry.Shape * entry.Texture) * _opacity;
        _target.SetPixel(x, y, _color.WithOpacity(alpha).Over(entry.Before));
        _dirty = _dirty.Union(new IntRect(x, y, 1, 1));
    }

    /// <summary>Returns the rectangle of pixels changed since the last call and resets it.</summary>
    public IntRect TakeDirty()
    {
        var dirty = _dirty;
        _dirty = IntRect.Empty;
        return dirty;
    }

    private static float Combine(float a, float b) => a == 0f ? b : 1f - (1f - a) * (1f - b);

    private struct Entry
    {
        public Rgba Before;
        public float Done;    // combined coverage of the finished passes
        public float Shape;   // highest shape coverage in the current pass
        public float Texture; // texture from the sweep least beyond its segment's ends
        public float Beyond;
        public double PassStart;
    }
}
