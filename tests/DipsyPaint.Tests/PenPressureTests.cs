using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Tests;

public class PenPressureTests
{
    [Theory]
    [InlineData(PointerType.Pen, 0.25f, 0.25f)]
    [InlineData(PointerType.Pen, 1f, 1f)]
    [InlineData(PointerType.Pen, 0f, 1f)]    // a pen without a pressure sensor
    [InlineData(PointerType.Pen, 1.5f, 1f)]
    [InlineData(PointerType.Mouse, 0.5f, 1f)] // Avalonia's placeholder for a mouse
    [InlineData(PointerType.Mouse, 0.2f, 1f)]
    [InlineData(PointerType.Touch, 0.5f, 1f)]
    public void OnlyAPensPressureCounts(PointerType type, float reported, float expected) =>
        Assert.Equal(expected, DocumentCanvas.PressureOf(type, reported));

    [AvaloniaFact]
    public void AMouseStroke_PaintsAsWithoutPressure()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        window.Editor.ActiveTool = new BrushTool(new StrokeSettings(15));
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        Point InWindow(double x, double y) => canvas.TranslatePoint(new Point(x, y), window)!.Value;

        window.MouseDown(InWindow(10, 24), MouseButton.Left);
        window.MouseMove(InWindow(30, 20));
        window.MouseMove(InWindow(50, 28));
        window.MouseUp(InWindow(50, 28), MouseButton.Left);

        var expected = new DocumentEditor(Document.CreateBlank(64, 48)) { ActiveTool = new BrushTool(new StrokeSettings(15)) };
        expected.Apply(new PointerInput(PointerAction.Down, 10, 24));
        expected.Apply(new PointerInput(PointerAction.Move, 30, 20));
        expected.Apply(new PointerInput(PointerAction.Move, 50, 28));
        expected.Apply(new PointerInput(PointerAction.Up, 50, 28));
        Assert.Equal(expected.Compositor.Pixels.ToArray(), window.Editor.Compositor.Pixels.ToArray());
    }
}
