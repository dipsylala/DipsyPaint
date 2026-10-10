using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;

namespace DipsyPaint.Tests;

public class HistoryTests
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

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static void PressCommand(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, Command(window), physicalKey, null);
        window.KeyRelease(key, Command(window), physicalKey, null);
    }

    private static void DrawHorizontalLine(Window window, double x0, double x1, double y)
    {
        window.MouseDown(InWindow(window, x0, y), MouseButton.Left);
        window.MouseMove(InWindow(window, x1, y));
        window.MouseUp(InWindow(window, x1, y), MouseButton.Left);
    }

    private static Button HistoryButton(Window window, string name) =>
        window.GetLogicalDescendants().OfType<HistoryControl>().Single().FindControl<Button>(name)!;

    private static void Click(Window window, Control control)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
    }

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
    public void CommandZUndoesAndCommandYRedoesAStroke()
    {
        var window = ShowWindow();
        var layer = window.Document.ActiveLayer.Pixels;
        DrawHorizontalLine(window, 10.5, 30.5, 20.5);
        Assert.Equal(Rgba.Black, layer.GetPixel(20, 20));

        PressCommand(window, Key.Z, PhysicalKey.Z);

        Assert.All(Enumerable.Range(10, 21), x => Assert.Equal(Rgba.White, layer.GetPixel(x, 20)));
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 20.5, 20.5)));

        PressCommand(window, Key.Y, PhysicalKey.Y);

        Assert.All(Enumerable.Range(10, 21), x => Assert.Equal(Rgba.Black, layer.GetPixel(x, 20)));
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 20.5, 20.5)));
    }

    [AvaloniaFact]
    public void UndoAndRedoButtonsFollowTheHistory()
    {
        var window = ShowWindow();
        var layer = window.Document.ActiveLayer.Pixels;
        var undo = HistoryButton(window, "UndoButton");
        var redo = HistoryButton(window, "RedoButton");
        Assert.False(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        DrawHorizontalLine(window, 5.5, 15.5, 5.5);
        Assert.True(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        Click(window, undo);
        Assert.Equal(Rgba.White, layer.GetPixel(10, 5));
        Assert.False(undo.IsEnabled);
        Assert.True(redo.IsEnabled);

        Click(window, redo);
        Assert.Equal(Rgba.Black, layer.GetPixel(10, 5));
        Assert.True(undo.IsEnabled);
        Assert.False(redo.IsEnabled);
    }
}
