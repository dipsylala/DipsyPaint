using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DipsyPaint.Images;

/// <summary>
/// The resize part of reference Paint's Resize and Skew dialog: a new size by percentage (default,
/// 100%) or in pixels, keeping the aspect ratio unless unticked. Skew is not offered yet (ticket 23).
/// Closes with the new size in pixels, or null if cancelled.
/// </summary>
public partial class ResizeDialog : Window
{
    private readonly int _width;
    private readonly int _height;
    private bool _updating;

    // The designer and XAML loader need a parameterless constructor.
    public ResizeDialog() : this(1, 1)
    {
    }

    /// <param name="width">The current width of what is being resized, in pixels.</param>
    /// <param name="height">The current height, in pixels.</param>
    public ResizeDialog(int width, int height)
    {
        InitializeComponent();
        (_width, _height) = (width, height);
        ShowValues();
        PercentageOption.IsCheckedChanged += (_, _) => ConvertValues();
        HorizontalBox.ValueChanged += (_, _) => KeepAspectRatio(HorizontalBox, VerticalBox, _width, _height);
        VerticalBox.ValueChanged += (_, _) => KeepAspectRatio(VerticalBox, HorizontalBox, _height, _width);
    }

    /// <summary>The size chosen, in pixels; null until OK is pressed.</summary>
    public (int Width, int Height)? Result { get; private set; }

    private bool ByPercentage => PercentageOption.IsChecked == true;

    private static int Value(NumericUpDown box) => (int)Math.Clamp(box.Value ?? 1, 1, int.MaxValue);

    private static int Scale(int value, int numerator, int denominator) =>
        (int)Math.Clamp(Math.Round((double)value * numerator / denominator, MidpointRounding.AwayFromZero), 1, int.MaxValue);

    private void ShowValues(int? horizontal = null, int? vertical = null)
    {
        _updating = true;
        HorizontalBox.Value = horizontal ?? (ByPercentage ? 100 : _width);
        VerticalBox.Value = vertical ?? (ByPercentage ? 100 : _height);
        _updating = false;
    }

    // Switching between percentage and pixels keeps the size that the boxes describe.
    private void ConvertValues()
    {
        var (horizontal, vertical) = (Value(HorizontalBox), Value(VerticalBox));
        if (ByPercentage)
        {
            ShowValues(Scale(horizontal, 100, _width), Scale(vertical, 100, _height));
        }
        else
        {
            ShowValues(Scale(_width, horizontal, 100), Scale(_height, vertical, 100));
        }
    }

    private void KeepAspectRatio(NumericUpDown changed, NumericUpDown other, int changedSide, int otherSide)
    {
        if (_updating || MaintainAspectRatioBox.IsChecked != true)
        {
            return;
        }

        _updating = true;
        other.Value = ByPercentage ? Value(changed) : Scale(otherSide, Value(changed), changedSide);
        _updating = false;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var (horizontal, vertical) = (Value(HorizontalBox), Value(VerticalBox));
        Result = ByPercentage ? (Scale(_width, horizontal, 100), Scale(_height, vertical, 100)) : (horizontal, vertical);
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
