using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class OpacityTests
{
    private static readonly string[] StrokeTools = [ToolStrings.Pencil, BrushStrings.Brush, BrushStrings.Eraser];

    private static MainWindow ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void SelectTool(Window window, string name)
    {
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    private static IEnumerable<Slider> Sliders(Window window, string name) =>
        window.GetVisualDescendants().OfType<Slider>().Where(s => AutomationProperties.GetName(s) == name && s.IsEffectivelyVisible);

    private static Slider OpacitySlider(Window window) => Sliders(window, OpacityStrings.Opacity).Single();

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        Point At((double X, double Y) p) => canvas.TranslatePoint(new Point(p.X, p.Y), window)!.Value;
        window.MouseDown(At(from), MouseButton.Left);
        window.MouseMove(At(to));
        window.MouseUp(At(to), MouseButton.Left);
    }

    /// <summary>The tools' settings are shared app-wide, so tests put every opacity back to 100 %.</summary>
    private static void ResetOpacities(Window window)
    {
        foreach (var tool in StrokeTools)
        {
            SelectTool(window, tool);
            OpacitySlider(window).Value = 100;
        }
    }

    [AvaloniaFact]
    public void Pencil_ShowsANamedOpacityControlOnly()
    {
        var window = ShowWindow();

        SelectTool(window, ToolStrings.Pencil);

        var slider = OpacitySlider(window);
        Assert.True(slider.Focusable);
        Assert.Equal(1, slider.Minimum);
        Assert.Equal(100, slider.Maximum);
        Assert.Equal(100, slider.Value);
        Assert.Empty(Sliders(window, BrushStrings.Size));
    }

    [AvaloniaTheory]
    [InlineData("Brush")]
    [InlineData("Eraser")]
    public void BrushAndEraser_ShowSizeAndOpacityControls(string tool)
    {
        var window = ShowWindow();

        SelectTool(window, tool == "Brush" ? BrushStrings.Brush : BrushStrings.Eraser);

        Assert.Single(Sliders(window, BrushStrings.Size));
        Assert.Equal(100, OpacitySlider(window).Value);
    }

    [AvaloniaFact]
    public void OpacityControl_SetsThePencilAndBrushStrokeOpacity()
    {
        var window = ShowWindow();
        try
        {
            SelectTool(window, ToolStrings.Pencil);
            OpacitySlider(window).Value = 50;
            Drag(window, (5, 10), (25, 10));
            SelectTool(window, BrushStrings.Brush);
            OpacitySlider(window).Value = 25;
            Drag(window, (5, 30), (25, 30));

            var pixels = window.Document.ActiveLayer.Pixels;
            Assert.Equal(new Rgba(127, 127, 127, 255), pixels.GetPixel(15, 10));
            Assert.Equal(new Rgba(191, 191, 191, 255), pixels.GetPixel(15, 30));
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "25%");
        }
        finally
        {
            ResetOpacities(window);
        }
    }

    [AvaloniaFact]
    public void EraserOpacity_LeavesPixelsPartiallyTransparent()
    {
        var window = ShowWindow();
        try
        {
            SelectTool(window, BrushStrings.Eraser);
            OpacitySlider(window).Value = 50;

            Drag(window, (10, 20), (40, 20));

            Assert.Equal(128, window.Document.ActiveLayer.Pixels.GetPixel(25, 20).A);
            window.Editor.Undo();
            Assert.Equal(Rgba.White, window.Document.ActiveLayer.Pixels.GetPixel(25, 20));
        }
        finally
        {
            ResetOpacities(window);
        }
    }

    [AvaloniaFact]
    public void EachToolKeepsItsOwnOpacity_AcrossReselection()
    {
        var window = ShowWindow();
        try
        {
            SelectTool(window, ToolStrings.Pencil);
            OpacitySlider(window).Value = 30;
            SelectTool(window, BrushStrings.Brush);
            OpacitySlider(window).Value = 60;
            SelectTool(window, BrushStrings.Eraser);
            OpacitySlider(window).Value = 90;

            SelectTool(window, ToolStrings.Pencil);
            Assert.Equal(30, OpacitySlider(window).Value);
            SelectTool(window, BrushStrings.Brush);
            Assert.Equal(60, OpacitySlider(window).Value);
            SelectTool(window, BrushStrings.Eraser);
            Assert.Equal(90, OpacitySlider(window).Value);
        }
        finally
        {
            ResetOpacities(window);
        }
    }
}
