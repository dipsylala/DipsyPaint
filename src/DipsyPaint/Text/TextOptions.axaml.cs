using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Text;

/// <summary>
/// The text tool's options panel, as reference Paint's text options: font family, font size (a listed
/// size, or any size typed in), bold, italic, underline, strikethrough, and whether the background is
/// filled with colour 2. Options apply to all of the pending text. After a pointer pick the keyboard
/// focus goes back to the text box, so typing carries on.
/// </summary>
public partial class TextOptions : UserControl
{
    private readonly TextSettings _settings;
    private readonly ObservableCollection<string> _fonts;
    private bool _showing;

    public TextOptions() : this(new TextSettings())
    {
    }

    public TextOptions(TextSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        if (string.IsNullOrWhiteSpace(settings.FontFamily))
        {
            settings.FontFamily = FontManager.Current.DefaultFontFamily.Name;
        }

        _fonts = new ObservableCollection<string>(FontManager.Current.SystemFonts.Select(f => f.Name).Distinct()
            .Order(StringComparer.CurrentCultureIgnoreCase));
        FontBox.ItemsSource = _fonts;
        SizeBox.ItemsSource = TextSettings.Sizes.Select(size => FormatSize(size)).ToList();

        FontBox.SelectionChanged += (_, _) => Apply(() => _settings.FontFamily = FontBox.SelectedItem as string ?? _settings.FontFamily);
        FontBox.DropDownClosed += (_, _) => FocusText();
        SizeBox.SelectionChanged += (_, _) =>
        {
            if (SizeBox.SelectedItem is string size)
            {
                Apply(() => SetSize(size));
            }
        };
        SizeBox.DropDownClosed += (_, _) => FocusText();
        SizeBox.KeyDown += OnSizeKeyDown;
        SizeBox.LostFocus += (_, _) => Apply(() => SetSize(SizeBox.Text));
        Toggle(BoldButton, on => _settings.Bold = on);
        Toggle(ItalicButton, on => _settings.Italic = on);
        Toggle(UnderlineButton, on => _settings.Underline = on);
        Toggle(StrikethroughButton, on => _settings.Strikethrough = on);
        Toggle(FillBackgroundButton, on => _settings.FillBackground = on);
        ShowSettings();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _settings.Changed += OnSettingsChanged;
        ShowSettings();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _settings.Changed -= OnSettingsChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private static string FormatSize(double size) => size.ToString("0.##", CultureInfo.CurrentCulture);

    // Ctrl+B and the like change the settings from the text box.
    private void OnSettingsChanged(object? sender, EventArgs e) => ShowSettings();

    private void ShowSettings()
    {
        _showing = true;
        // A family that isn't a listed system font (e.g. the platform's hidden default) is listed first.
        if (!_fonts.Contains(_settings.FontFamily))
        {
            _fonts.Insert(0, _settings.FontFamily);
        }

        FontBox.SelectedItem = _settings.FontFamily;
        var size = FormatSize(_settings.Size);
        SizeBox.SelectedItem = SizeBox.Items.OfType<string>().FirstOrDefault(s => s == size);
        SizeBox.Text = size;
        BoldButton.IsChecked = _settings.Bold;
        ItalicButton.IsChecked = _settings.Italic;
        UnderlineButton.IsChecked = _settings.Underline;
        StrikethroughButton.IsChecked = _settings.Strikethrough;
        FillBackgroundButton.IsChecked = _settings.FillBackground;
        _showing = false;
    }

    private void Toggle(ToggleButton button, Action<bool> set) =>
        button.Click += (_, _) =>
        {
            set(button.IsChecked == true);
            FocusText();
        };

    private void Apply(Action change)
    {
        if (!_showing)
        {
            change();
        }
    }

    // A size that isn't a number is ignored, and the current size shown again.
    private void SetSize(string? text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size))
        {
            _settings.Size = size;
        }

        ShowSettings();
    }

    private void OnSizeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetSize(SizeBox.Text);
            FocusText();
            e.Handled = true;
        }
    }

    private void FocusText() =>
        TopLevel.GetTopLevel(this)?.GetVisualDescendants().OfType<TextEntry>().FirstOrDefault()?.FocusText();
}
