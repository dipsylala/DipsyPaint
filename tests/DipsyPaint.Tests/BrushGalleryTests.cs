using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

public class BrushGalleryTests
{
    private sealed class OtherBrush(StrokeSettings settings) : ITool
    {
        public StrokeSettings Settings { get; } = settings;

        public IntRect Handle(ToolContext context, PointerInput input) => IntRect.Empty;
    }

    private static readonly StrokeSettings Settings = new(size: 5);

    private static BrushGallery TwoBrushes() => new(
    [
        new("brush", BrushStrings.Brush, "M0 0h24v24H0z", s => new BrushTool(s)),
        new("other", "Other", "M0 0h24v24H0z", s => new OtherBrush(s)),
    ]);

    private static (ToolPicker Picker, DocumentEditor Editor, ToolDescriptor Brush) ShowPicker(BrushGallery gallery)
    {
        var brush = new ToolDescriptor("brush", BrushStrings.Brush, "M0 0h24v24H0z", null,
            () => gallery.Selected.CreateTool(Settings), () => new BrushOptions(Settings, gallery));
        var fill = new ToolDescriptor("fill", ToolStrings.Fill, "M0 0h24v24H0z", null, () => new FillTool());
        var picker = new ToolPicker([brush, fill]);
        var editor = new DocumentEditor(Document.CreateBlank(8, 8));
        var window = new Window { Content = picker, DataContext = editor };
        window.Show();
        Click(picker, BrushStrings.Brush);
        return (picker, editor, brush);
    }

    private static void Click(Control root, string name)
    {
        root.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        (TopLevel.GetTopLevel(root) as Window)?.UpdateLayout();
    }

    private static ComboBox BrushMenu(Control root) =>
        root.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == BrushStrings.Brushes);

    [AvaloniaFact]
    public void TheBrushTool_StartsWithTheBasicBrush_AndShowsTheBrushMenu()
    {
        var window = new MainWindow(Document.CreateBlank(8, 8));
        window.Show();
        window.UpdateLayout();
        Click(window, BrushStrings.Brush);

        Assert.True(BrushMenu(window).IsEffectivelyVisible);
        Assert.IsType<BrushTool>(window.Editor.ActiveTool);
        Assert.Contains(window.GetVisualDescendants().OfType<Slider>(), s => s.IsEffectivelyVisible && AutomationProperties.GetName(s) == BrushStrings.Size);
    }

    [AvaloniaFact]
    public void WithOneBrush_TheBrushMenuIsHidden()
    {
        var (picker, editor, _) = ShowPicker(new BrushGallery([new("brush", BrushStrings.Brush, "M0 0h24v24H0z", s => new BrushTool(s))]));

        Assert.False(BrushMenu(picker).IsEffectivelyVisible);
        Assert.IsType<BrushTool>(editor.ActiveTool);
    }

    [AvaloniaFact]
    public void ChoosingABrush_MakesItTheActiveTool_WithTheSharedSettings()
    {
        var gallery = TwoBrushes();
        var (picker, editor, _) = ShowPicker(gallery);
        var menu = BrushMenu(picker);
        Assert.True(menu.IsEffectivelyVisible);
        Assert.Equal(["Brush", "Other"], menu.Items.Cast<ComboBoxItem>().Select(AutomationProperties.GetName));

        menu.SelectedIndex = 1;

        Assert.Equal("other", gallery.Selected.Id);
        Assert.Same(Settings, Assert.IsType<OtherBrush>(editor.ActiveTool).Settings);
    }

    [AvaloniaFact]
    public void TheChosenBrush_SurvivesSwitchingTools()
    {
        var gallery = TwoBrushes();
        var (picker, editor, _) = ShowPicker(gallery);
        BrushMenu(picker).SelectedIndex = 1;

        Click(picker, ToolStrings.Fill);
        Click(picker, BrushStrings.Brush);

        Assert.IsType<OtherBrush>(editor.ActiveTool);
        Assert.Equal(1, BrushMenu(picker).SelectedIndex);
    }

    [Fact]
    public void AGallery_OnlySelectsItsOwnBrushes()
    {
        var gallery = TwoBrushes();

        Assert.Throws<ArgumentException>(() => gallery.Selected = new BrushDescriptor("x", "X", "M0 0", s => new BrushTool(s)));
    }
}
