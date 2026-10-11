using Avalonia;
using Avalonia.Controls;
using DipsyPaint.Files;

namespace DipsyPaint.Images;

/// <summary>What the Resize and skew dialog asks for: a new size in pixels, then skew angles in degrees.</summary>
public readonly record struct ResizeAndSkew(int Width, int Height, int HorizontalSkew = 0, int VerticalSkew = 0);

/// <summary>The dialogs behind the Image commands. Tests substitute their own to bypass real windows.</summary>
public interface IImageDialogs
{
    /// <summary>
    /// Asks for a new size and skew for something <paramref name="width"/>×<paramref name="height"/> px;
    /// null if the user cancels.
    /// </summary>
    Task<ResizeAndSkew?> AskResizeAsync(int width, int height);

    /// <summary>Asks for an angle to turn by, in degrees clockwise (Custom rotate); null if the user cancels.</summary>
    Task<int?> AskRotationAsync();

    Task ShowErrorAsync(string message);
}

/// <summary>Shows <see cref="ResizeDialog"/>, <see cref="CustomRotateDialog"/> and the File menu's message box, modal to the window.</summary>
internal sealed class WindowImageDialogs(Visual anchor) : IImageDialogs
{
    public async Task<ResizeAndSkew?> AskResizeAsync(int width, int height) =>
        TopLevel.GetTopLevel(anchor) is Window owner
            ? await new ResizeDialog(width, height).ShowDialog<ResizeAndSkew?>(owner)
            : null;

    public async Task<int?> AskRotationAsync() =>
        TopLevel.GetTopLevel(anchor) is Window owner
            ? await new CustomRotateDialog().ShowDialog<int?>(owner)
            : null;

    public Task ShowErrorAsync(string message) => new StorageProviderDialogs(anchor).ShowErrorAsync(message);
}
