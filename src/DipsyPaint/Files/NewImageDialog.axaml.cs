using Avalonia.Controls;
using Avalonia.Interactivity;
using DipsyPaint.Engine;

namespace DipsyPaint.Files;

/// <summary>
/// Asks for the size of a new image, in pixels, limited to <see cref="Document.MaxSide"/> per side.
/// An addition beyond reference Paint, whose New doesn't ask (ticket 34). Closes with the size, or
/// null if cancelled.
/// </summary>
public partial class NewImageDialog : Window
{
    // The designer and XAML loader need a parameterless constructor.
    public NewImageDialog() : this(Document.DefaultWidth, Document.DefaultHeight)
    {
    }

    /// <param name="width">The width to prefill, in pixels.</param>
    /// <param name="height">The height to prefill, in pixels.</param>
    public NewImageDialog(int width, int height)
    {
        InitializeComponent();
        WidthBox.Maximum = HeightBox.Maximum = Document.MaxSide;
        WidthBox.Value = width;
        HeightBox.Value = height;
    }

    /// <summary>The size chosen, in pixels; null until OK is pressed.</summary>
    public (int Width, int Height)? Result { get; private set; }

    private static int Value(NumericUpDown box) => (int)Math.Clamp(box.Value ?? 1, 1, Document.MaxSide);

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = (Value(WidthBox), Value(HeightBox));
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
