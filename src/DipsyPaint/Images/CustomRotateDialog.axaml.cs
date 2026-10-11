using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DipsyPaint.Images;

/// <summary>
/// Reference Paint's Custom rotate (Rotate menu): an angle in whole degrees, clockwise, from −360 to 360.
/// Closes with the angle, or null if cancelled.
/// </summary>
public partial class CustomRotateDialog : Window
{
    public CustomRotateDialog()
    {
        InitializeComponent();
        Opened += (_, _) => AngleBox.Focus();
    }

    /// <summary>The angle chosen; null until OK is pressed.</summary>
    public int? Result { get; private set; }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = (int)Math.Clamp(AngleBox.Value ?? 0, -360, 360);
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
