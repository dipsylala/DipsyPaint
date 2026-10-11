using Avalonia.Controls;
using Avalonia.Interactivity;
using DipsyPaint.Engine.Codecs;

namespace DipsyPaint.Files;

/// <summary>
/// Reference Paint's warning before saving to a palette format (GIF, or a 256-colour, 16-colour or
/// monochrome bitmap; <see cref="ImageCodec.UsesPalette"/>), which loses colour information (ADR-0019).
/// Closes with true for OK, false for Cancel.
/// </summary>
public partial class LossySaveWarning : Window
{
    public LossySaveWarning()
    {
        InitializeComponent();
    }

    /// <summary>True once OK is pressed.</summary>
    public bool Result { get; private set; }

    /// <summary>
    /// Asks before saving as <paramref name="format"/>; true to save. Formats that keep every colour
    /// save without asking.
    /// </summary>
    public static async Task<bool> ConfirmAsync(IFileDialogs dialogs, ImageFormat format) =>
        !ImageCodec.UsesPalette(format) || await dialogs.ConfirmLossySaveAsync();

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
