namespace DipsyPaint.Engine;

/// <summary>
/// The only way to read or write a layer's pixels (ADR-0005). Tools and other engine code
/// never touch layer storage directly, so storage can change (e.g. to tiles) without
/// rewriting them. Coordinates are in document pixels; out-of-bounds access throws.
/// </summary>
public interface IPixelAccess
{
    int Width { get; }
    int Height { get; }

    Rgba GetPixel(int x, int y);
    void SetPixel(int x, int y, Rgba value);

    /// <summary>Copies <paramref name="region"/> row by row into <paramref name="destination"/>.</summary>
    void ReadRegion(IntRect region, Span<Rgba> destination);

    /// <summary>Overwrites <paramref name="region"/> with row-by-row pixels from <paramref name="source"/>.</summary>
    void WriteRegion(IntRect region, ReadOnlySpan<Rgba> source);
}
