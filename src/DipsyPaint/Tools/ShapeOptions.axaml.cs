using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// The shape tool's options panel, as reference Paint's Shapes group: the shape gallery, outline
/// and fill styles (outline in colour 1, fill in colour 2) and outline size. Option changes apply
/// to the pending shape; picking another shape commits it.
/// </summary>
public partial class ShapeOptions : UserControl
{
    // Bespoke icons (the Fluent set has no plain line/rectangle/oval shapes), ADR-0012.
    private static readonly (ShapeKind Kind, string Name, string Icon)[] Gallery =
    [
        (ShapeKind.Line, ShapeStrings.Line, "M4.47 18.47l14-14 1.06 1.06-14 14z"),
        (ShapeKind.Rectangle, ShapeStrings.Rectangle, "M3 5h18v14H3zM4.5 6.5v11h15v-11z"),
        (ShapeKind.Ellipse, ShapeStrings.Ellipse,
            "M12 5c5 0 9 3.13 9 7s-4 7-9 7-9-3.13-9-7 4-7 9-7zm0 1.5c-4.14 0-7.5 2.46-7.5 5.5s3.36 5.5 7.5 5.5 7.5-2.46 7.5-5.5-3.36-5.5-7.5-5.5z"),
    ];

    private readonly ShapeSettings _settings;
    private readonly Dictionary<ShapeKind, ToggleButton> _shapeButtons = [];

    public ShapeOptions() : this(new ShapeSettings())
    {
    }

    public ShapeOptions(ShapeSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        foreach (var (kind, name, icon) in Gallery)
        {
            var button = new ToggleButton { Content = new PathIcon { Data = Geometry.Parse(icon) }, Width = 32, Height = 32 };
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => SelectShape(kind);
            ShapeButtons.Children.Add(button);
            _shapeButtons[kind] = button;
        }

        SizeBox.ItemsSource = ShapeSettings.Sizes.Select(size => string.Format(ShapeStrings.SizeFormat, size)).ToList();
        OutlineBox.SelectedIndex = (int)settings.Outline;
        FillBox.SelectedIndex = (int)settings.Fill;
        SizeBox.SelectedIndex = Math.Max(0, ShapeSettings.Sizes.ToList().IndexOf(settings.Size));
        UpdateShapeButtons();
    }

    private DocumentEditor? Editor => DataContext as DocumentEditor;

    private void SelectShape(ShapeKind kind)
    {
        Editor?.CommitPending();
        _settings.Kind = kind;
        UpdateShapeButtons();
    }

    private void UpdateShapeButtons()
    {
        foreach (var (kind, button) in _shapeButtons)
        {
            button.IsChecked = kind == _settings.Kind;
        }

        FillBox.IsEnabled = _settings.Kind != ShapeKind.Line; // a line has no inside
    }

    private void OnOutlineChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Outline = (ShapeStyle)OutlineBox.SelectedIndex, OutlineBox);

    private void OnFillChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Fill = (ShapeStyle)FillBox.SelectedIndex, FillBox);

    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Size = ShapeSettings.Sizes[SizeBox.SelectedIndex], SizeBox);

    private void Apply(Action change, ComboBox box)
    {
        if (box.SelectedIndex < 0)
        {
            return;
        }

        change();
        Editor?.RefreshPending();
    }
}
