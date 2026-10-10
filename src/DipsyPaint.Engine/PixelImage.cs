namespace DipsyPaint.Engine;

/// <summary>
/// A standalone rectangle of pixels outside any document, such as clipboard content: row-major,
/// premultiplied, <see cref="Width"/> × <see cref="Height"/>.
/// </summary>
public sealed class PixelImage
{
    public PixelImage(int width, int height, Rgba[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfNotEqual(pixels.Length, width * height, nameof(pixels));
        (Width, Height, Pixels) = (width, height, pixels);
    }

    public int Width { get; }
    public int Height { get; }
    public Rgba[] Pixels { get; }
}
