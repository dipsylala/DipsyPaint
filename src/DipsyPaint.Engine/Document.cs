namespace DipsyPaint.Engine;

/// <summary>An open image being edited, with a fixed canvas size.</summary>
public sealed class Document
{
    /// <summary>Largest allowed canvas side, in pixels (ADR-0005).</summary>
    public const int MaxSide = 16384;

    // Placeholder until reference Paint's default is confirmed by the parity audit.
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;

    public int Width { get; }
    public int Height { get; }

    private Document(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static Document CreateBlank(int width = DefaultWidth, int height = DefaultHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSide);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSide);
        return new Document(width, height);
    }
}
