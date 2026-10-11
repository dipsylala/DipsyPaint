using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint;

/// <summary>How the status bar shows a document's resolution (ADR-0024).</summary>
public static class ResolutionText
{
    /// <summary>
    /// "300 dpi", or "204 × 196 dpi" when the directions differ, to one decimal place; with
    /// <paramref name="fileValueWasInvalid"/>, "96 dpi (file's value was invalid)".
    /// </summary>
    public static string Format(Resolution resolution, bool fileValueWasInvalid = false)
    {
        var (horizontal, vertical) = (Math.Round(resolution.HorizontalDpi, 1), Math.Round(resolution.VerticalDpi, 1));
        if (fileValueWasInvalid)
        {
            return string.Format(ResolutionStrings.InvalidDpiFormat, horizontal);
        }

        return horizontal == vertical
            ? string.Format(ResolutionStrings.DpiFormat, horizontal)
            : string.Format(ResolutionStrings.DpiPairFormat, horizontal, vertical);
    }
}
