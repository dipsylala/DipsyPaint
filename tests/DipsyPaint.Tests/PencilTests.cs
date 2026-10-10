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
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

public class PencilTests
{
    private static MainWindow ShowWindow(int width = 64, int height = 48)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static ToggleButton PencilButton(Window window) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == ToolStrings.Pencil);

    /// <summary>Reads one pixel of the rendered frame as (R, G, B).</summary>
    private static (byte R, byte G, byte B) RenderedPixel(Window window, Point point)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var bytes = new byte[4];
        var offset = (int)point.Y * buffer.RowBytes + (int)point.X * 4;
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address + offset, bytes, 0, 4);
        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888 ? (bytes[2], bytes[1], bytes[0]) : (bytes[0], bytes[1], bytes[2]);
    }

    [AvaloniaFact]
    public void CommandBarShowsThePencilFromTheRegistry()
    {
        var window = ShowWindow();

        var button = PencilButton(window);
        var descriptor = ToolRegistry.Tools.Single(t => t.Id == "pencil");
        Assert.Equal(descriptor.Shortcut, button.HotKey);
        Assert.True(button.IsChecked);
        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void ShortcutSelectsThePencil()
    {
        var window = ShowWindow();
        window.Editor.ActiveTool = null;

        window.KeyPress(Key.P, RawInputModifiers.None, PhysicalKey.P, "p");
        window.KeyRelease(Key.P, RawInputModifiers.None, PhysicalKey.P, "p");

        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void DraggingWithThePencilDrawsOnTheCanvas()
    {
        var window = ShowWindow();
        var start = InWindow(window, 10.5, 20.5);
        var end = InWindow(window, 30.5, 20.5);
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 20.5, 20.5)));

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end);
        window.MouseUp(end, MouseButton.Left);

        var layer = window.Document.ActiveLayer.Pixels;
        Assert.All(Enumerable.Range(10, 21), x => Assert.Equal(Rgba.Black, layer.GetPixel(x, 20)));
        Assert.Equal(Rgba.White, layer.GetPixel(10, 21));
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 20.5, 20.5)));
    }

    [AvaloniaFact]
    public void RightButtonDrawsInTheSecondaryColour()
    {
        var window = ShowWindow();
        window.Editor.SecondaryColor = new Rgba(0, 0, 255, 255);
        var point = InWindow(window, 5.5, 5.5);

        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Equal(new Rgba(0, 0, 255, 255), window.Document.ActiveLayer.Pixels.GetPixel(5, 5));
    }
}
