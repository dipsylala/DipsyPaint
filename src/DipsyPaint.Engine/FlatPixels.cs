namespace DipsyPaint.Engine;

/// <summary>Layer storage as one flat, row-major bitmap (ADR-0005).</summary>
internal sealed class FlatPixels : IPixelAccess
{
    private readonly Rgba[] _pixels;

    public FlatPixels(int width, int height, Rgba fill)
    {
        Width = width;
        Height = height;
        _pixels = GC.AllocateUninitializedArray<Rgba>(width * height);
        Array.Fill(_pixels, fill);
    }

    public int Width { get; }
    public int Height { get; }

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
