using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// Options panel with a size slider for a stroke tool (brush, eraser), followed by its
/// <see cref="StrokeOpacityOptions"/>. While shown, it also handles
/// reference Paint's size shortcuts for the whole window: Ctrl+Plus and Ctrl+Minus (Cmd on macOS)
/// change the size by one pixel.
/// </summary>
public partial class StrokeSizeOptions : UserControl
{
    private readonly StrokeSettings _settings;
    private TopLevel? _topLevel;

    public StrokeSizeOptions() : this(new StrokeSettings(StrokeSettings.MinSize))
    {
    }

    public StrokeSizeOptions(StrokeSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        SizeSlider.Minimum = StrokeSettings.MinSize;
        SizeSlider.Maximum = StrokeSettings.MaxSize;
        SizeSlider.Value = settings.Size;
        SizeSlider.ValueChanged += (_, e) => SetSize((int)Math.Round(e.NewValue));
        UpdateSizeText();
        Items.Children.Add(new StrokeOpacityOptions(settings));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Shift is ignored because some layouts need it to type "+".
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != CommandModifiers)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Add or Key.OemPlus:
                SetSize(_settings.Size + 1);
                e.Handled = true;
                break;
            case Key.Subtract or Key.OemMinus:
                SetSize(_settings.Size - 1);
                e.Handled = true;
                break;
        }
    }

    private void SetSize(int size)
    {
        _settings.Size = size;
        SizeSlider.Value = _settings.Size;
        UpdateSizeText();
    }

    private void UpdateSizeText() => SizeText.Text = string.Format(BrushStrings.SizeFormat, _settings.Size);
}
