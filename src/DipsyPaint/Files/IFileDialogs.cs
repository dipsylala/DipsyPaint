using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint.Files;

/// <summary>
/// A file the user picked: its display name and how to read or overwrite it. <paramref name="Format"/>
/// is the file type chosen in the Save dialog, when the platform reports it.
/// </summary>
public sealed record PickedFile(string Name, Func<Task<Stream>> OpenReadAsync, Func<Task<Stream>> OpenWriteAsync, ImageFormat? Format = null);

/// <summary>The dialogs behind the File commands. Tests substitute their own to bypass native dialogs.</summary>
public interface IFileDialogs
{
    /// <summary>Asks for an image to open; null if the user cancels.</summary>
    Task<PickedFile?> PickOpenAsync();

    /// <summary>Asks where to save, offering <paramref name="format"/> first; null if the user cancels.</summary>
    Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format);

    /// <summary>
    /// Asks for a new image's size, prefilled with <paramref name="width"/>×<paramref name="height"/> px,
    /// and returns it with the resolution chosen (ADR-0024); null if the user cancels.
    /// </summary>
    Task<(int Width, int Height, Resolution Resolution)?> AskNewImageSizeAsync(int width, int height);

    Task ShowErrorAsync(string message);

    /// <summary>Warns that a palette format loses colour information (<see cref="LossySaveWarning"/>); true to save anyway.</summary>
    Task<bool> ConfirmLossySaveAsync();
}
