using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DipsyPaint.Colours;
using DipsyPaint.Engine;
using DipsyPaint.Layout;
using DipsyPaint.Resources;

namespace DipsyPaint;

/// <summary>
/// Command-bar colour group, as in reference Paint: the Colour 1 (primary) and Colour 2
/// (secondary) swatches of the <see cref="DocumentEditor"/> in the DataContext, a swap button and
/// the default palette, a row of custom colours and Edit colours. Left-clicking (or pressing) a
/// palette or custom swatch sets the selected colour slot; right-clicking one always sets Colour 2.
/// Edit colours sets the selected slot to the colour chosen and adds it to the custom colours,
/// which last for the session. On a side edge the palette's 2 × 10 grid turns into 10 × 2, each of
/// its rows becoming a column, and the custom colours become a third column.
/// </summary>
public partial class ColourControl : UserControl
{
    /// <summary>How many custom colours the Colours group holds, as in reference Paint: one row.</summary>
    public const int CustomColourSlots = 10;

    private readonly List<Button> _swatches = [];
    private readonly List<Button> _customSwatches = [];
    private readonly List<Rgba> _customColours = [];
    private DocumentEditor? _editor;
    private bool _colour2Selected;

    public ColourControl()
    {
        InitializeComponent();
        foreach (var (name, color) in Palette)
        {
            var swatch = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(2),
                Content = new Border { Background = ToBrush(color), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) },
            };
            AutomationProperties.SetName(swatch, name);
            ToolTip.SetTip(swatch, name);
            swatch.Click += (_, _) => SetColor(_colour2Selected, color);
            swatch.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right)
                {
                    SetColor(secondary: true, color);
                    e.Handled = true;
                }
            }, handledEventsToo: true);
            _swatches.Add(swatch);
        }

        for (var i = 0; i < CustomColourSlots; i++)
        {
            var slot = i;
            var swatch = new Button { Width = 20, Height = 20, Padding = new Thickness(2) };
            swatch.Click += (_, _) => SetColor(_colour2Selected, _customColours[slot]);
            swatch.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right && slot < _customColours.Count)
                {
                    SetColor(secondary: true, _customColours[slot]);
                    e.Handled = true;
                }
            }, handledEventsToo: true);
            _customSwatches.Add(swatch);
            CustomColoursGrid.Children.Add(swatch);
        }

        Dialogs = new WindowColourDialogs(this);
        ArrangePalette(CommandGroup.GetOrientation(this));
        UpdateSwatches();
        UpdateCustomSwatches();
    }

    /// <summary>The dialogs behind Edit colours; tests substitute their own.</summary>
    public IColourDialogs Dialogs { get; set; }

    /// <summary>The custom colours added this session, oldest first, at most <see cref="CustomColourSlots"/>.</summary>
    public IReadOnlyList<Rgba> CustomColours => _customColours;

    /// <summary>
    /// Reference Paint's default palette: two rows of ten, top row first. These are classic Paint's
    /// (Windows 7) values, which reference Paint is believed to keep; confirm in the parity audit.
    /// </summary>
    public static IReadOnlyList<(string Name, Rgba Color)> Palette { get; } =
    [
        (ColourStrings.Black, new Rgba(0, 0, 0, 255)),
        (ColourStrings.Grey50, new Rgba(127, 127, 127, 255)),
        (ColourStrings.DarkRed, new Rgba(136, 0, 21, 255)),
        (ColourStrings.Red, new Rgba(237, 28, 36, 255)),
        (ColourStrings.Orange, new Rgba(255, 127, 39, 255)),
        (ColourStrings.Yellow, new Rgba(255, 242, 0, 255)),
        (ColourStrings.Green, new Rgba(34, 177, 76, 255)),
        (ColourStrings.Turquoise, new Rgba(0, 162, 232, 255)),
        (ColourStrings.Indigo, new Rgba(63, 72, 204, 255)),
        (ColourStrings.Purple, new Rgba(163, 73, 164, 255)),
        (ColourStrings.White, new Rgba(255, 255, 255, 255)),
        (ColourStrings.Grey25, new Rgba(195, 195, 195, 255)),
        (ColourStrings.Brown, new Rgba(185, 122, 87, 255)),
        (ColourStrings.Rose, new Rgba(255, 174, 201, 255)),
        (ColourStrings.Gold, new Rgba(255, 201, 14, 255)),
        (ColourStrings.LightYellow, new Rgba(239, 228, 176, 255)),
        (ColourStrings.Lime, new Rgba(181, 230, 29, 255)),
        (ColourStrings.LightTurquoise, new Rgba(153, 217, 234, 255)),
        (ColourStrings.BlueGrey, new Rgba(112, 146, 190, 255)),
        (ColourStrings.Lavender, new Rgba(200, 191, 231, 255)),
    ];

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        var previous = _editor;
        _editor?.ColorsChanged -= OnColorsChanged;
        _editor = DataContext as DocumentEditor;
        if (previous is not null && _editor is not null)
        {
            // Reference Paint keeps the colours when another document is opened.
            _editor.PrimaryColor = previous.PrimaryColor;
            _editor.SecondaryColor = previous.SecondaryColor;
        }

        _editor?.ColorsChanged += OnColorsChanged;
        UpdateSwatches();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandGroup.OrientationProperty)
        {
            ArrangePalette(change.GetNewValue<Orientation>());
        }
    }

    private static IBrush ToBrush(Rgba color)
    {
        var straight = color.ToOpaque();
        return new SolidColorBrush(Color.FromArgb(color.A, straight.R, straight.G, straight.B));
    }

    // Horizontal: two rows of ten, as Palette. Vertical: ten rows of two, the top row becoming the
    // left column; the grid fills row by row, so the swatches go in as (top[i], bottom[i]) pairs.
    private void ArrangePalette(Orientation orientation)
    {
        var vertical = orientation == Orientation.Vertical;
        PalettePanel.Orientation = vertical ? Orientation.Horizontal : Orientation.Vertical;
        CustomColoursGrid.Rows = vertical ? CustomColourSlots : 1;
        CustomColoursGrid.Columns = vertical ? 1 : CustomColourSlots;
        var half = _swatches.Count / 2;
        PaletteGrid.Rows = vertical ? half : 2;
        PaletteGrid.Columns = vertical ? 2 : half;
        PaletteGrid.Children.Clear();
        PaletteGrid.Children.AddRange(vertical
            ? Enumerable.Range(0, half).SelectMany(i => new[] { _swatches[i], _swatches[i + half] })
            : _swatches);
    }

    private void SetColor(bool secondary, Rgba color)
    {
        if (_editor is null)
        {
            return;
        }

        if (secondary)
        {
            _editor.SecondaryColor = color;
        }
        else
        {
            _editor.PrimaryColor = color;
        }
    }

    private void OnColorsChanged(object? sender, EventArgs e) => UpdateSwatches();

    /// <summary>
    /// Adds <paramref name="color"/> to the custom colours: into the first empty slot or, when all
    /// are full, at the end with the oldest dropped.
    /// </summary>
    public void AddCustomColour(Rgba color)
    {
        if (_customColours.Count == CustomColourSlots)
        {
            _customColours.RemoveAt(0);
        }

        _customColours.Add(color);
        UpdateCustomSwatches();
    }

    // Empty slots show as outlines and can't be clicked or focused.
    private void UpdateCustomSwatches()
    {
        for (var i = 0; i < _customSwatches.Count; i++)
        {
            var swatch = _customSwatches[i];
            var filled = i < _customColours.Count;
            var name = filled
                ? string.Format(CultureInfo.CurrentCulture, ColourEditorStrings.CustomColour, EditColoursDialog.Hex(_customColours[i]))
                : ColourEditorStrings.EmptyCustomColour;
            swatch.IsEnabled = filled;
            swatch.Content = new Border
            {
                Background = filled ? ToBrush(_customColours[i]) : null,
                BorderBrush = filled ? Brushes.Gray : Brushes.LightGray,
                BorderThickness = new Thickness(1),
            };
            AutomationProperties.SetName(swatch, name);
            ToolTip.SetTip(swatch, name);
        }
    }

    private async void OnEditColoursClick(object? sender, RoutedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        var selected = _colour2Selected ? _editor.SecondaryColor : _editor.PrimaryColor;
        if (await Dialogs.EditColoursAsync(selected) is not { } color)
        {
            return;
        }

        SetColor(_colour2Selected, color);
        AddCustomColour(color);
    }

    private void UpdateSwatches()
    {
        Colour1Button.IsChecked = !_colour2Selected;
        Colour2Button.IsChecked = _colour2Selected;
        Colour1Swatch.Background = _editor is null ? null : ToBrush(_editor.PrimaryColor);
        Colour2Swatch.Background = _editor is null ? null : ToBrush(_editor.SecondaryColor);
    }

    private void OnColour1Click(object? sender, RoutedEventArgs e)
    {
        _colour2Selected = false;
        UpdateSwatches();
    }

    private void OnColour2Click(object? sender, RoutedEventArgs e)
    {
        _colour2Selected = true;
        UpdateSwatches();
    }

    private void OnSwapClick(object? sender, RoutedEventArgs e)
    {
        if (_editor is not null)
        {
            (_editor.PrimaryColor, _editor.SecondaryColor) = (_editor.SecondaryColor, _editor.PrimaryColor);
        }
    }
}
