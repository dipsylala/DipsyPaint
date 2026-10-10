namespace DipsyPaint.Engine.Codecs;

/// <summary>A flat file format the engine can open and export (ADR-0019).</summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    /// <summary>BMP/DIB; exported as 24-bit.</summary>
    Bmp,
}
