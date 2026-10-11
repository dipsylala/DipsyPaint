namespace DipsyPaint.Engine.Tools;

/// <summary>Text to render for a <see cref="TextTool"/>.</summary>
/// <param name="Text">The text; may be empty, and may contain line breaks.</param>
/// <param name="Width">Width of the text box in pixels (at least 1); lines wrap to it.</param>
/// <param name="MinHeight">Height of the text box in pixels (at least 1); the image is taller when the text needs it.</param>
/// <param name="Foreground">Text colour (premultiplied).</param>
/// <param name="Background">Colour the whole box is filled with first, or null for a transparent background.</param>
public sealed record TextRequest(string Text, TextStyle Style, int Width, int MinHeight, Rgba Foreground, Rgba? Background);

/// <summary>
/// Lays out and rasterises text. The engine has no font engine of its own; the shell supplies one
/// built on its UI toolkit's text layout, so the rendered text lines up with the shell's caret.
/// </summary>
public interface ITextRenderer
{
    /// <summary>
    /// Renders <see cref="TextRequest.Text"/> from the top-left corner, wrapped at
    /// <see cref="TextRequest.Width"/>, into premultiplied pixels exactly <see cref="TextRequest.Width"/>
    /// wide and as tall as the larger of <see cref="TextRequest.MinHeight"/> and the laid-out text
    /// (at least one line, even when the text is empty).
    /// </summary>
    PixelImage Render(TextRequest request);
}
