using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class FillTests
{
    [AvaloniaFact]
    public void ClickingWithTheFillToolFillsTheRegion()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var fillButton = window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == ToolStrings.Fill);

        var buttonCentre = fillButton.TranslatePoint(new Point(fillButton.Bounds.Width / 2, fillButton.Bounds.Height / 2), window)!.Value;
        window.MouseDown(buttonCentre, MouseButton.Left);
        window.MouseUp(buttonCentre, MouseButton.Left);
        Assert.IsType<FillTool>(window.Editor.ActiveTool);
        Assert.True(fillButton.IsChecked);
        window.UpdateLayout(); // the command bar may rewrap for the new tool's options, moving the canvas
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        var point = canvas.TranslatePoint(new Point(20.5, 20.5), window)!.Value;

        window.Editor.PrimaryColor = new Rgba(255, 0, 0, 255);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        var layer = window.Document.ActiveLayer.Pixels;
        Assert.Equal(new Rgba(255, 0, 0, 255), layer.GetPixel(0, 0));
        Assert.Equal(new Rgba(255, 0, 0, 255), layer.GetPixel(63, 47));
        Assert.True(window.Editor.History.CanUndo);
    }
}
