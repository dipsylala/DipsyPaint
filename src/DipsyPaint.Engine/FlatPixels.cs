namespace DipsyPaint.Engine;

/// <summary>Layer storage as one flat, row-major bitmap (ADR-0005).</summary>
internal sealed class FlatPixels : IPixelAccess
{
    private Rgba[] _pixels;

    public FlatPixels(int width, int height, Rgba fill)
    {
        Width = width;
        Height = height;
        _pixels = GC.AllocateUninitializedArray<Rgba>(width * height);
        Array.Fill(_pixels, fill);
    }

    /// <summary>A copy of <paramref name="source"/>'s pixels.</summary>
    public FlatPixels(FlatPixels source)
    {
        Width = source.Width;
        Height = source.Height;
        _pixels = (Rgba[])source._pixels.Clone();
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>The pixels, row-major; for whole-layer operations inside the engine (see <see cref="Document"/>).</summary>
    internal Rgba[] Buffer => _pixels;

    /// <summary>Takes <paramref name="pixels"/> as the new <paramref name="width"/>×<paramref name="height"/> storage; returns the old pixels.</summary>
    internal Rgba[] Replace(int width, int height, Rgba[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(pixels.Length, width * height, nameof(pixels));
        var old = _pixels;
        (_pixels, Width, Height) = (pixels, width, height);
        return old;
    }

    public Rgba GetPixel(int x, int y) => _pixels[IndexOf(x, y)];

    public void SetPixel(int x, int y, Rgba value) => _pixels[IndexOf(x, y)] = value;

    public void ReadRegion(IntRect region, Span<Rgba> destination)
    {
        CheckRegion(region, destination.Length);
        for (var row = 0; row < region.Height; row++)
        {
            _pixels.AsSpan((region.Y + row) * Width + region.X, region.Width)
                .CopyTo(destination.Slice(row * region.Width, region.Width));
        }
    }

    public void WriteRegion(IntRect region, ReadOnlySpan<Rgba> source)
    {
        CheckRegion(region, source.Length);
        for (var row = 0; row < region.Height; row++)
        {
            source.Slice(row * region.Width, region.Width)
                .CopyTo(_pixels.AsSpan((region.Y + row) * Width + region.X, region.Width));
        }
    }

    private int IndexOf(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) is outside the {Width}×{Height} layer.");
        }

        return y * Width + x;
    }

    private void CheckRegion(IntRect region, int bufferLength)
    {
        if (region.IsEmpty || region.Intersect(new IntRect(0, 0, Width, Height)) != region)
        {
            throw new ArgumentOutOfRangeException(nameof(region), $"{region} is empty or outside the {Width}×{Height} layer.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, region.Width * region.Height, "bufferLength");
    }
}
