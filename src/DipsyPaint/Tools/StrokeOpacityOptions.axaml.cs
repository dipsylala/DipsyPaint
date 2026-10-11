using Avalonia.Controls;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// Options panel with an opacity slider (percent) for a stroke tool. The pencil shows it on its own;
/// <see cref="StrokeSizeOptions"/> includes it after the size slider.
/// </summary>
public partial class StrokeOpacityOptions : UserControl
{
    private readonly StrokeSettings _settings;

    public StrokeOpacityOptions() : this(new StrokeSettings(StrokeSettings.MinSize))
    {
    }

    public StrokeOpacityOptions(StrokeSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        OpacitySlider.Minimum = StrokeSettings.MinOpacity;
        OpacitySlider.Maximum = StrokeSettings.MaxOpacity;
        OpacitySlider.Value = settings.Opacity;
        OpacitySlider.ValueChanged += (_, e) => SetOpacity((int)Math.Round(e.NewValue));
        UpdateOpacityText();
    }

    private void SetOpacity(int opacity)
    {
        _settings.Opacity = opacity;
        OpacitySlider.Value = _settings.Opacity;
        UpdateOpacityText();
    }

    private void UpdateOpacityText() => OpacityText.Text = string.Format(OpacityStrings.OpacityFormat, _settings.Opacity);
}
