using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>The magic wand in the Selection menu and its tolerance slider (ticket 22).</summary>
public sealed class MagicWandTests : IDisposable
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba NearRed = new(230, 0, 0, 255); // 25 from red: 10% (26 per channel) or more

    // The selection options are app-wide, so each test starts and ends with the defaults.
    public MagicWandTests() => ResetSettings();

    public void Dispose() => ResetSettings();

    private static void ResetSettings()
    {
        ToolRegistry.SelectionSettings.Shape = SelectionShape.Rectangle;
        ToolRegistry.SelectionSettings.Transparent = false;
        ToolRegistry.SelectionSettings.Tolerance = SelectionSettings.DefaultTolerance;
    }

    /// <summary>
    /// Shows a 64×48 white document with a red 20×20 square at (10, 10) and a near-red column beside
    /// it at x = 30 (rows 10–29); the pencil is active.
    /// </summary>
    private static MainWindow ShowWindow()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(30, 10, 1, 20), Enumerable.Repeat(NearRed, 20).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static MenuItem WandItem(Window window) =>
        window.GetLogicalDescendants().OfType<SelectionMenu>().Single().FindControl<MenuItem>("MagicWandItem")!;

    private static MenuItem RectangleItem(Window window) =>
        window.GetLogicalDescendants().OfType<SelectionMenu>().Single().FindControl<MenuItem>("RectangleItem")!;

    // Then lays out, as the options panel may appear.
    private static void Click(Window window, MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
    }

    private static ToggleButton SelectToolButton(Window window) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == SelectionStrings.Select);

    private static MagicWandOptions? Options(Window window) => window.GetLogicalDescendants().OfType<MagicWandOptions>().SingleOrDefault();

    private static void ClickCanvas(Window window, double x, double y)
    {
        var point = window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void MagicWandItem_IsANamedShapeInTheSelectionMenu_AndSwitchesToTheSelectTool()
    {
        var window = ShowWindow();
        var item = WandItem(window);
        Assert.Equal(MagicWandStrings.MagicWand, AutomationProperties.GetName(item));
        Assert.Equal(MenuItemToggleType.Radio, item.ToggleType);
        Assert.False(item.IsChecked);

        Click(window, item);

        Assert.Equal(SelectionShape.MagicWand, ToolRegistry.SelectionSettings.Shape);
        Assert.True(item.IsChecked);
        Assert.False(RectangleItem(window).IsChecked);
        Assert.IsType<SelectionTool>(window.Editor.ActiveTool);
        Assert.True(SelectToolButton(window).IsChecked);
    }

    [AvaloniaFact]
    public void ToleranceSlider_IsShownOnlyForTheMagicWand()
    {
        var window = ShowWindow();
        SelectToolButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        var options = Assert.IsType<MagicWandOptions>(Options(window));
        Assert.False(options.IsEffectivelyVisible); // rectangle selection: no options, as in reference Paint

        Click(window, WandItem(window));
        Assert.True(options.IsEffectivelyVisible);

        Click(window, RectangleItem(window));
        Assert.False(options.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ToleranceSlider_SetsTheTolerance_ShowingItInPercent()
    {
        var window = ShowWindow();
        Click(window, WandItem(window));
        var options = Options(window)!;
        var slider = options.FindControl<Slider>("ToleranceSlider")!;
        Assert.Equal(MagicWandStrings.Tolerance, AutomationProperties.GetName(slider));
        Assert.Equal((0, 100, 12), (slider.Minimum, slider.Maximum, slider.Value));

        slider.Value = 40;

        Assert.Equal(40, ToolRegistry.SelectionSettings.Tolerance);
        Assert.Equal(string.Format(MagicWandStrings.ToleranceFormat, 40), options.FindControl<TextBlock>("ToleranceText")!.Text);
    }

    [AvaloniaFact]
    public void ClickingSelectsTheAreaWithinTheTolerance_AndDeleteRemovesIt()
    {
        var window = ShowWindow();
        Click(window, WandItem(window));
        Options(window)!.FindControl<Slider>("ToleranceSlider")!.Value = 0;

        ClickCanvas(window, 15.5, 15.5);
        Assert.Equal(new Selection(new IntRect(10, 10, 20, 20), null), window.Editor.Selection);

        Options(window)!.FindControl<Slider>("ToleranceSlider")!.Value = 10;
        ClickCanvas(window, 50.5, 40.5); // the white background, framed by the whole canvas
        Assert.Equal(new IntRect(0, 0, 64, 48), window.Editor.Selection?.Bounds);
        ClickCanvas(window, 15.5, 15.5); // unselected, inside the frame: a new selection

        Assert.Equal(new Selection(new IntRect(10, 10, 21, 20), null), window.Editor.Selection);

        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Rgba.Transparent, window.Document.ActiveLayer.Pixels.GetPixel(30, 29));
        Assert.Equal(Rgba.White, window.Document.ActiveLayer.Pixels.GetPixel(31, 29));
    }
}
