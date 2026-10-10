using Avalonia;
using Avalonia.Controls;
using DipsyPaint.Files;

namespace DipsyPaint.Images;

/// <summary>The dialogs behind the Image commands. Tests substitute their own to bypass real windows.</summary>
public interface IImageDialogs
{
    /// <summary>Asks for a new size for something <paramref name="width"/>×<paramref name="height"/> px; null if the user cancels.</summary>
    Task<(int Width, int Height)?> AskResizeAsync(int width, int height);

    Task ShowErrorAsync(string message);
}

/// <summary>Shows <see cref="ResizeDialog"/> and the File menu's message box, modal to the window.</summary>
internal sealed class WindowImageDialogs(Visual anchor) : IImageDialogs
{
    public async Task<(int Width, int Height)?> AskResizeAsync(int width, int height) =>
        TopLevel.GetTopLevel(anchor) is Window owner
            ? await new ResizeDialog(width, height).ShowDialog<(int Width, int Height)?>(owner)
            : null;

    public Task ShowErrorAsync(string message) => new StorageProviderDialogs(anchor).ShowErrorAsync(message);
}
