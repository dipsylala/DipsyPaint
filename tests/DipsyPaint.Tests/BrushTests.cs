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
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class BrushTests
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

    private static void SelectTool(Window window, string name)
    {
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    /// <summary>The size slider of the active tool's options panel.</summary>
    private static Slider SizeSlider(Window window) =>
        window.GetVisualDescendants().OfType<Slider>().Single(s => AutomationProperties.GetName(s) == BrushStrings.Size);

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static int PaintedRows(MainWindow window, int x) =>
        Enumerable.Range(0, window.Document.Height).Count(y => window.Document.ActiveLayer.Pixels.GetPixel(x, y) != Rgba.White);

    private static void PressCommand(Window window, Key key, PhysicalKey physicalKey)
    {
        var command = (RawInputModifiers)window.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;
        window.KeyPress(key, command, physicalKey, null);
        window.KeyRelease(key, command, physicalKey, null);
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
    public void SelectingTheBrush_ShowsANamedSizeControl()
    {
        var window = ShowWindow();

        SelectTool(window, BrushStrings.Brush);

        Assert.IsType<BrushTool>(window.Editor.ActiveTool);
        var slider = SizeSlider(window);
        Assert.True(slider.Focusable);
        Assert.Equal(StrokeSettings.MinSize, slider.Minimum);
        Assert.Equal(StrokeSettings.MaxSize, slider.Maximum);
    }

    [AvaloniaFact]
    public void SizeControl_ChangesTheBrushStrokeWidth()
    {
        var window = ShowWindow();
        SelectTool(window, BrushStrings.Brush);

        SizeSlider(window).Value = 3;
        Drag(window, (5, 10), (25, 10));
        SizeSlider(window).Value = 15;
        Drag(window, (35, 24), (55, 24));

        Assert.InRange(PaintedRows(window, 15), 3, 5);
        Assert.InRange(PaintedRows(window, 45), 15, 17);
    }

    [AvaloniaFact]
    public void CtrlPlusAndMinus_ChangeTheActiveToolsSize()
    {
        var window = ShowWindow();
        SelectTool(window, BrushStrings.Brush);
        SizeSlider(window).Value = 10;

        PressCommand(window, Key.Add, PhysicalKey.NumPadAdd);
        PressCommand(window, Key.OemPlus, PhysicalKey.Equal);
        Assert.Equal(12, SizeSlider(window).Value);

        PressCommand(window, Key.Subtract, PhysicalKey.NumPadSubtract);
        Assert.Equal(11, SizeSlider(window).Value);
    }

    [AvaloniaFact]
    public void EachToolKeepsItsOwnSize_AcrossReselection()
    {
        var window = ShowWindow();
        SelectTool(window, BrushStrings.Brush);
        SizeSlider(window).Value = 7;
        SelectTool(window, BrushStrings.Eraser);
        SizeSlider(window).Value = 20;

        SelectTool(window, ToolStrings.Pencil);
        SelectTool(window, BrushStrings.Brush);

        Assert.Equal(7, SizeSlider(window).Value);
    }

    [AvaloniaFact]
    public void ShortcutSelectsTheEraser()
    {
        var window = ShowWindow();
        window.Editor.ActiveTool = null;

        window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
        window.KeyRelease(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");

        Assert.IsType<EraserTool>(window.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void Eraser_LeavesTransparencyShownAsACheckerboard()
    {
        var window = ShowWindow();
        SelectTool(window, BrushStrings.Eraser);
        SizeSlider(window).Value = 20;

        Drag(window, (10, 20), (40, 20));

        Assert.Equal(Rgba.Transparent, window.Document.ActiveLayer.Pixels.GetPixel(25, 20));
        // Squares of 8 document pixels: (16..23, 16..23) is grey, (24..31, 16..23) is white.
        Assert.Equal((0xCC, 0xCC, 0xCC), RenderedPixel(window, InWindow(window, 20.5, 20.5)));
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 28.5, 20.5)));

        window.Editor.Undo();
        Assert.Equal(Rgba.White, window.Document.ActiveLayer.Pixels.GetPixel(25, 20));
    }
}
