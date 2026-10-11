using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Tools;

/// <summary>
/// The Brush tool's options panel: a menu of the brushes in a <see cref="BrushGallery"/>, as in
/// reference Paint's Brushes menu, followed by the <see cref="StrokeSizeOptions"/> the brushes
/// share. Choosing a brush makes it the active tool. The menu is hidden while there is only one brush.
/// </summary>
public partial class BrushOptions : UserControl
{
    // A menu entry: the brush's icon and name.
    private static readonly FuncDataTemplate<BrushDescriptor> MenuEntry = new((brush, _) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            Icon(brush),
            new TextBlock { Text = brush.DisplayName, VerticalAlignment = VerticalAlignment.Center },
        },
    });

    // The closed menu shows only the chosen brush's icon, so the options fit beside the tool buttons.
    private static readonly FuncDataTemplate<BrushDescriptor> ChosenEntry = new((brush, _) => Icon(brush));

    private readonly StrokeSettings _settings;
    private readonly BrushGallery _gallery;

    public BrushOptions() : this(new StrokeSettings(StrokeSettings.MinSize), new BrushGallery([new("brush", "", "M0 0", s => new BrushTool(s))]))
    {
    }

    public BrushOptions(StrokeSettings settings, BrushGallery gallery)
    {
        _settings = settings;
        _gallery = gallery;
        InitializeComponent();
        foreach (var brush in gallery.Brushes)
        {
            var item = new ComboBoxItem { Tag = brush, Content = brush, ContentTemplate = MenuEntry };
            AutomationProperties.SetName(item, brush.DisplayName);
            BrushBox.Items.Add(item);
        }

        BrushBox.SelectionBoxItemTemplate = ChosenEntry;

        BrushBox.SelectedIndex = gallery.Brushes.ToList().IndexOf(gallery.Selected);
        BrushBox.IsVisible = gallery.Brushes.Count > 1;
        BrushBox.SelectionChanged += (_, _) => Choose((BrushBox.SelectedItem as ComboBoxItem)?.Tag as BrushDescriptor);
        Items.Children.Add(new StrokeSizeOptions(settings));
    }

    private static PathIcon Icon(BrushDescriptor brush) => new() { Data = Geometry.Parse(brush.IconData), Width = 16, Height = 16 };

    private void Choose(BrushDescriptor? brush)
    {
        if (brush is null || brush == _gallery.Selected)
        {
            return;
        }

        _gallery.Selected = brush;
        // The panel is shown only while the Brush tool is active, so the new brush takes over now.
        (DataContext as DocumentEditor)?.ActiveTool = brush.CreateTool(_settings);
    }
}
