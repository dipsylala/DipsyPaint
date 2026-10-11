using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>
/// Asks for the size of a new image, in pixels, limited to <see cref="Document.MaxSide"/> per side,
/// typed or picked from <see cref="NewImagePreset"/>s, and shows its resolution read-only. An addition
/// beyond reference Paint, whose New doesn't ask (tickets 34 and 41, ADR-0024). Closes with the size
/// and resolution, or null if cancelled.
/// </summary>
public partial class NewImageDialog : Window
{
    private readonly ComboBoxItem _custom;
    private Resolution _resolution = Resolution.Default;

    // Set while the dialog itself changes Width and Height, so only the user's edits select Custom.
    private bool _settingSides;

    // The designer and XAML loader need a parameterless constructor.
    public NewImageDialog() : this(Document.DefaultWidth, Document.DefaultHeight)
    {
    }

    /// <param name="width">The width to prefill, in pixels.</param>
    /// <param name="height">The height to prefill, in pixels.</param>
    /// <remarks>Selects the preset of that size at 96 dpi, if there is one, and Custom otherwise.</remarks>
    public NewImageDialog(int width, int height)
    {
        InitializeComponent();
        WidthBox.Maximum = HeightBox.Maximum = Document.MaxSide;

        foreach (var (heading, presets) in NewImagePreset.Groups())
        {
            if (heading is not null)
            {
                // Disabled, so the dropdown's keyboard selection skips it.
                SizeBox.Items.Add(Entry(heading, tag: null, isHeading: true));
            }

            foreach (var preset in presets)
            {
                SizeBox.Items.Add(Entry(preset.Text, preset));
            }
        }

        _custom = Entry(NewImageStrings.Custom, tag: null);
        SizeBox.Items.Add(_custom);

        // A closed ComboBox opens on Enter; here Enter means OK, as everywhere else in the dialog.
        // Space, F4 and Alt+Down still open it.
        SizeBox.AddHandler(KeyDownEvent, OnSizeKeyDown, RoutingStrategies.Tunnel);

        SetSides(width, height);
        ShowResolution();
        SizeBox.SelectedItem = SizeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            item.Tag is NewImagePreset preset && (preset.Width, preset.Height) == (width, height) && preset.Resolution == Resolution.Default)
            ?? _custom;
    }

    /// <summary>The size, in pixels, and resolution chosen; null until OK is pressed.</summary>
    public (int Width, int Height, Resolution Resolution)? Result { get; private set; }

    private static ComboBoxItem Entry(string text, NewImagePreset? tag, bool isHeading = false)
    {
        var item = new ComboBoxItem { Content = text, Tag = tag };
        AutomationProperties.SetName(item, text);
        if (isHeading)
        {
            item.IsEnabled = false;
            item.FontWeight = FontWeight.SemiBold;
        }

        return item;
    }

    private static int Value(NumericUpDown box) => (int)Math.Clamp(box.Value ?? 1, 1, Document.MaxSide);

    private void SetSides(int width, int height)
    {
        _settingSides = true;
        WidthBox.Value = width;
        HeightBox.Value = height;
        _settingSides = false;
    }

    private void ShowResolution() => ResolutionValue.Text = ResolutionText.Format(_resolution);

    // Choosing a preset fills in its size and resolution; choosing Custom keeps the current values.
    private void OnSizeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SizeBox.SelectedItem is ComboBoxItem { Tag: NewImagePreset preset })
        {
            SetSides(preset.Width, preset.Height);
            _resolution = preset.Resolution;
            ShowResolution();
        }
    }

    private void OnSizeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !SizeBox.IsDropDownOpen)
        {
            e.Handled = true;
            OnOkClick(sender, e);
        }
    }

    // Typing a size is no longer the preset's size, but keeps its resolution.
    private void OnSideChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_settingSides)
        {
            SizeBox.SelectedItem = _custom;
        }
    }

    // Keeps the preset (A4 swapped is A4 landscape) and the resolution. Provisional (ticket 41).
    private void OnSwapClick(object? sender, RoutedEventArgs e) => SetSides(Value(HeightBox), Value(WidthBox));

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = (Value(WidthBox), Value(HeightBox), _resolution);
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
