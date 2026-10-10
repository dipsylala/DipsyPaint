using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint;

/// <summary>The OS clipboard and message box behind the Clipboard commands. Tests substitute their own.</summary>
public interface IImageClipboard
{
    /// <summary>Puts <paramref name="image"/> on the clipboard in place of what was there.</summary>
    /// <exception cref="IOException">The clipboard could not be written.</exception>
    Task SetImageAsync(PixelImage image);

    /// <summary>The image on the clipboard, or null if it holds none.</summary>
    /// <exception cref="ImageOpenException">The clipboard holds an image that could not be read.</exception>
    Task<PixelImage?> GetImageAsync();

    Task ShowErrorAsync(string message);
}
