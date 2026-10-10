namespace DipsyPaint.Engine;

/// <summary>
/// One 8-bit sRGB pixel with premultiplied alpha, laid out R, G, B, A in memory
/// (Skia's and Avalonia's <c>Rgba8888</c> premultiplied format, ADR-0005).
/// </summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static Rgba Transparent => default;
    public static Rgba Black => new(0, 0, 0, 255);
    public static Rgba White => new(255, 255, 255, 255);

    /// <summary>Composites this pixel over <paramref name="background"/> (Porter-Duff source-over).</summary>
    public Rgba Over(Rgba background)
    {
        var inverse = 255 - A;
        return new Rgba(
            (byte)(R + Div255(background.R * inverse)),
            (byte)(G + Div255(background.G * inverse)),
            (byte)(B + Div255(background.B * inverse)),
            (byte)(A + Div255(background.A * inverse)));
    }

    /// <summary>Scales every channel by <paramref name="opacity"/> (0–1), keeping the pixel premultiplied.</summary>
    public Rgba WithOpacity(float opacity)
    {
        var scale = (int)MathF.Round(Math.Clamp(opacity, 0f, 1f) * 255f);
        return new Rgba((byte)Div255(R * scale), (byte)Div255(G * scale), (byte)Div255(B * scale), (byte)Div255(A * scale));
    }

    // Exact rounded division by 255 for 0 <= value <= 255 * 255.
    private static int Div255(int value) => (value + 128 + ((value + 128) >> 8)) >> 8;
}
