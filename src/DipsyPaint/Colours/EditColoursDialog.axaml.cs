using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Colours;

/// <summary>
/// Reference Paint's Edit colours dialog: a colour wheel (hue around, saturation outwards), a
/// brightness slider, a preview, the colour as RGB or HSV fields and as hex, and OK/Cancel. Every
/// part edits the same colour and shows the others' changes. Colours are opaque, as in Paint.
/// Closes with the colour chosen, or null if cancelled.
/// </summary>
public partial class EditColoursDialog : Window
{
    private HsvColor _colour;
    private bool _updating;

    // The designer and XAML loader need a parameterless constructor.
    public EditColoursDialog() : this(Rgba.Black)
    {
    }

    /// <param name="initial">The colour to start from; its opacity is ignored.</param>
    public EditColoursDialog(Rgba initial)
    {
        InitializeComponent();
        var straight = initial.ToOpaque();
        Spectrum.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSpectrum.HsvColorProperty)
            {
                SetColour(Spectrum.HsvColor, Spectrum);
            }
        };
        BrightnessSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSlider.HsvColorProperty)
            {
                SetColour(BrightnessSlider.HsvColor, BrightnessSlider);
            }
        };
        Box1.ValueChanged += (_, _) => OnFieldChanged();
        Box2.ValueChanged += (_, _) => OnFieldChanged();
        Box3.ValueChanged += (_, _) => OnFieldChanged();
        HexBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                OnHexChanged();
            }
        };
        HexBox.LostFocus += (_, _) => HexBox.Text = Hex(Colour);
        ModelBox.SelectionChanged += (_, _) => ShowModel();
        ShowModel();
        SetColour(Color.FromRgb(straight.R, straight.G, straight.B).ToHsv(), source: null);
    }

    /// <summary>The colour being edited, opaque.</summary>
    public Rgba Colour
    {
        get
        {
            var rgb = _colour.ToRgb();
            return new Rgba(rgb.R, rgb.G, rgb.B, 255);
        }
    }

    /// <summary>The colour chosen; null until OK is pressed.</summary>
    public Rgba? Result { get; private set; }

    private bool IsHsv => ModelBox.SelectedIndex == 1;

    /// <summary>The colour as reference Paint's hex field shows it: <c>#RRGGBB</c>.</summary>
    public static string Hex(Rgba colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

    /// <summary>Parses <c>RRGGBB</c>, with or without a leading <c>#</c>; null if it isn't one.</summary>
    public static Rgba? ParseHex(string? text)
    {
        var digits = text?.Trim().TrimStart('#');
        return digits is { Length: 6 } && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? new Rgba((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255)
            : null;
    }

    private static int Field(NumericUpDown box) => (int)(box.Value ?? 0);

    // Shows the colour everywhere except in the part it came from, so a field being typed in or a
    // slider being dragged isn't rewritten under the user.
    private void SetColour(HsvColor colour, Control? source)
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        _colour = new HsvColor(1, colour.H, colour.S, colour.V);
        if (source != Spectrum)
        {
            Spectrum.HsvColor = _colour;
        }

        if (source != BrightnessSlider)
        {
            BrightnessSlider.HsvColor = _colour;
        }

        if (source is not NumericUpDown)
        {
            ShowFields();
        }

        if (source != HexBox)
        {
            HexBox.Text = Hex(Colour);
        }

        var rgb = Colour;
        Preview.Background = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
        _updating = false;
    }

    private void ShowFields()
    {
        if (IsHsv)
        {
            Box1.Value = (int)Math.Round(_colour.H) % 360;
            Box2.Value = (int)Math.Round(_colour.S * 100);
            Box3.Value = (int)Math.Round(_colour.V * 100);
        }
        else
        {
            var rgb = Colour;
            (Box1.Value, Box2.Value, Box3.Value) = (rgb.R, rgb.G, rgb.B);
        }
    }

    // Relabels the three fields for RGB (0–255) or HSV (hue 0–359°, saturation and value 0–100%).
    private void ShowModel()
    {
        var hsv = IsHsv;
        string[] names = hsv
            ? [ColourEditorStrings.Hue, ColourEditorStrings.Saturation, ColourEditorStrings.Value]
            : [ColourEditorStrings.Red, ColourEditorStrings.Green, ColourEditorStrings.Blue];
        decimal[] maxima = hsv ? [359, 100, 100] : [255, 255, 255];
        Label[] labels = [Label1, Label2, Label3];
        NumericUpDown[] boxes = [Box1, Box2, Box3];
        var wasUpdating = _updating;
        _updating = true;
        for (var i = 0; i < 3; i++)
        {
            labels[i].Content = names[i];
            AutomationProperties.SetName(boxes[i], names[i]);
            boxes[i].Maximum = maxima[i];
        }

        ShowFields();
        _updating = wasUpdating;
    }

    private void OnFieldChanged()
    {
        if (_updating)
        {
            return;
        }

        var colour = IsHsv
            ? new HsvColor(1, Field(Box1), Field(Box2) / 100.0, Field(Box3) / 100.0)
            : Color.FromRgb((byte)Field(Box1), (byte)Field(Box2), (byte)Field(Box3)).ToHsv();
        SetColour(colour, Box1);
    }

    private void OnHexChanged()
    {
        if (!_updating && ParseHex(HexBox.Text) is { } rgb)
        {
            SetColour(Color.FromRgb(rgb.R, rgb.G, rgb.B).ToHsv(), HexBox);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = Colour;
        Close(Result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
