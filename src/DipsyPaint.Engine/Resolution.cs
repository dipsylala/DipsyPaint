namespace DipsyPaint.Engine;

/// <summary>The unit a <see cref="Resolution"/> counts pixels per.</summary>
public enum ResolutionUnit
{
    PixelsPerInch,
    PixelsPerCentimetre,
    PixelsPerMetre,
}

/// <summary>
/// A document's <i>resolution</i>: how many pixels per unit other programs should use when printing or
/// placing the image (ADR-0024). Metadata only; it never changes the pixels. It is kept in the unit
/// the file stored it in (e.g. PNG's pixels per metre), so re-saving writes the same values back.
/// </summary>
/// <param name="Horizontal">Pixels per <paramref name="Unit"/> across.</param>
/// <param name="Vertical">Pixels per <paramref name="Unit"/> down.</param>
/// <param name="Unit">What the values count pixels per; dots per inch (DPI) by default.</param>
public readonly record struct Resolution(double Horizontal, double Vertical, ResolutionUnit Unit = ResolutionUnit.PixelsPerInch)
{
    /// <summary>The smallest resolution a file may state, in DPI per direction; smaller values mean <see cref="Default"/>.</summary>
    public const double MinDpi = 1;

    /// <summary>The largest resolution a file may state, in DPI per direction; larger values mean <see cref="Default"/>.</summary>
    public const double MaxDpi = 10_000;

    /// <summary>96 × 96 DPI: new images, and files without resolution information.</summary>
    public static Resolution Default { get; } = new(96, 96);

    /// <summary>The horizontal resolution in dots (pixels) per inch.</summary>
    public double HorizontalDpi => ToDpi(Horizontal);

    /// <summary>The vertical resolution in dots (pixels) per inch.</summary>
    public double VerticalDpi => ToDpi(Vertical);

    /// <summary>Whether both directions are from <see cref="MinDpi"/> to <see cref="MaxDpi"/>.</summary>
    public bool IsValid => IsValidDpi(HorizontalDpi) && IsValidDpi(VerticalDpi);

    // False for NaN as well.
    private static bool IsValidDpi(double dpi) => dpi is >= MinDpi and <= MaxDpi;

    private double ToDpi(double value) => Unit switch
    {
        ResolutionUnit.PixelsPerCentimetre => value * 2.54,
        ResolutionUnit.PixelsPerMetre => value * 0.0254,
        _ => value,
    };
}
