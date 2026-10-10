using DipsyPaint.Engine.Codecs;

namespace DipsyPaint.Files;

/// <summary>A file the user picked: its display name and how to read or overwrite it.</summary>
public sealed record PickedFile(string Name, Func<Task<Stream>> OpenReadAsync, Func<Task<Stream>> OpenWriteAsync);

/// <summary>The dialogs behind the File commands. Tests substitute their own to bypass native dialogs.</summary>
public interface IFileDialogs
{
    /// <summary>Asks for an image to open; null if the user cancels.</summary>
    Task<PickedFile?> PickOpenAsync();

    /// <summary>Asks where to save, offering <paramref name="format"/> first; null if the user cancels.</summary>
    Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format);

    Task ShowErrorAsync(string message);
}
