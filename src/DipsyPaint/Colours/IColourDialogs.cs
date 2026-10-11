using Avalonia;
using Avalonia.Controls;
using DipsyPaint.Engine;

namespace DipsyPaint.Colours;

/// <summary>The dialogs behind the Colours group. Tests substitute their own to bypass real windows.</summary>
public interface IColourDialogs
{
    /// <summary>Shows Edit colours starting from <paramref name="initial"/>; the colour chosen, or null if the user cancels.</summary>
    Task<Rgba?> EditColoursAsync(Rgba initial);
}

/// <summary>Shows <see cref="EditColoursDialog"/> modal to the window.</summary>
internal sealed class WindowColourDialogs(Visual anchor) : IColourDialogs
{
    public async Task<Rgba?> EditColoursAsync(Rgba initial) =>
        TopLevel.GetTopLevel(anchor) is Window owner
            ? await new EditColoursDialog(initial).ShowDialog<Rgba?>(owner)
            : null;
}
