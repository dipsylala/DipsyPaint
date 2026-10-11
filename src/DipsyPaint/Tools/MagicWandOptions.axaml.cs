using Avalonia;
using Avalonia.Controls;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// The Select tool's options panel: the magic wand's colour tolerance slider (percent,
/// <see cref="SelectionSettings.Tolerance"/>), shown only while the selection shape is
/// <see cref="SelectionShape.MagicWand"/>; reference Paint's other shapes have no options.
/// </summary>
public partial class MagicWandOptions : UserControl
{
    private readonly SelectionSettings _settings;

    public MagicWandOptions() : this(new SelectionSettings())
    {
    }

    public MagicWandOptions(SelectionSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        ToleranceSlider.Minimum = SelectionSettings.MinTolerance;
        ToleranceSlider.Maximum = SelectionSettings.MaxTolerance;
        ToleranceSlider.Value = settings.Tolerance;
        ToleranceSlider.ValueChanged += (_, e) => SetTolerance((int)Math.Round(e.NewValue));
        UpdateToleranceText();
        UpdateVisibility();
    }

    // The settings are app-wide and outlive the panel, so it listens only while shown.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _settings.ShapeChanged += OnShapeChanged;
        UpdateVisibility();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _settings.ShapeChanged -= OnShapeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnShapeChanged(object? sender, EventArgs e) => UpdateVisibility();

    private void UpdateVisibility() => IsVisible = _settings.Shape == SelectionShape.MagicWand;

    private void SetTolerance(int tolerance)
    {
        _settings.Tolerance = tolerance;
        ToleranceSlider.Value = _settings.Tolerance;
        UpdateToleranceText();
    }

    private void UpdateToleranceText() => ToleranceText.Text = string.Format(MagicWandStrings.ToleranceFormat, _settings.Tolerance);
}
