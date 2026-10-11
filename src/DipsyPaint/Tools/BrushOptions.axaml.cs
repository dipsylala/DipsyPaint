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
        // Like reference Paint's Brushes button, the closed menu shows only the chosen brush's icon,
        // which keeps the tools row narrow; the open menu lists each brush's icon and name.
        BrushBox.SelectionBoxItemTemplate = new FuncDataTemplate<BrushDescriptor>((brush, _) => Icon(brush));
        var entry = new FuncDataTemplate<BrushDescriptor>((brush, _) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Icon(brush),
                new TextBlock { Text = brush.DisplayName, VerticalAlignment = VerticalAlignment.Center },
            },
        });
        foreach (var brush in gallery.Brushes)
        {
            var item = new ComboBoxItem { Tag = brush, Content = brush, ContentTemplate = entry };
            AutomationProperties.SetName(item, brush.DisplayName);
            BrushBox.Items.Add(item);
        }

        BrushBox.SelectedIndex = gallery.Brushes.ToList().IndexOf(gallery.Selected);
        BrushBox.IsVisible = gallery.Brushes.Count > 1;
        BrushBox.SelectionChanged += (_, _) => Choose((BrushBox.SelectedItem as ComboBoxItem)?.Tag as BrushDescriptor);
        Items.Children.Add(new StrokeSizeOptions(settings));
    }

    private static PathIcon Icon(BrushDescriptor? brush) =>
        new() { Data = brush is null ? null : Geometry.Parse(brush.IconData), Width = 16, Height = 16 };

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
