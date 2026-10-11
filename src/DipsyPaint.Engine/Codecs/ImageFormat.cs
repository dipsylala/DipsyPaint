namespace DipsyPaint.Engine.Codecs;

/// <summary>
/// A file type the engine can open or save (ADR-0019), as listed in reference Paint's Open and
/// Save dialogs: flat image formats, plus <see cref="OpenRaster"/>, the layered project format.
/// The BMP variants differ only in how they are exported; all of them open.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    /// <summary>BMP/DIB; exported as 24-bit.</summary>
    Bmp,
    Gif,
    Tiff,
    /// <summary>Opens only; reference Paint cannot save icons.</summary>
    Ico,
    /// <summary>Opens only; reference Paint cannot save WebP.</summary>
    Webp,
    /// <summary>BMP exported with a 256-colour palette.</summary>
    Bmp256,
    /// <summary>BMP exported with a 16-colour palette.</summary>
    Bmp16,
    /// <summary>BMP exported in black and white, 1 bit per pixel.</summary>
    BmpMonochrome,
    /// <summary>OpenRaster <c>.ora</c>, the project file format; keeps layers (ADR-0006).</summary>
    OpenRaster,
}
