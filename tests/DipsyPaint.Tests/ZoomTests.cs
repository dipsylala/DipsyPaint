using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DipsyPaint.Engine;

namespace DipsyPaint.Tests;

public class ZoomTests
{
    private static MainWindow ShowWindow(int width = 640, int height = 480)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        window.UpdateLayout();
        return window;
    }

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static ZoomControl Zoom(Window window) => window.FindControl<ZoomControl>("Zoom")!;

    private static string ZoomText(Window window) => Zoom(window)
        .FindControl<TextBlock>("ZoomText")!.Text!;

    private static Border Canvas(Window window) => window.FindControl<Border>("Canvas")!;

    private static ScrollViewer Scroller(Window window) => window.FindControl<ScrollViewer>("CanvasScroll")!;

    /// <summary>The canvas's on-screen rectangle in window coordinates, after zoom.</summary>
    private static Rect RenderedRect(Window window)
    {
        var canvas = Canvas(window);
        var topLeft = canvas.TranslatePoint(default, window)!.Value;
        var bottomRight = canvas.TranslatePoint(new Point(canvas.Bounds.Width, canvas.Bounds.Height), window)!.Value;
        return new Rect(topLeft, bottomRight);
    }

    private static Point CentreOf(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, string buttonName)
    {
        var point = CentreOf(window, Zoom(window).FindControl<Button>(buttonName)!);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
    }

    private static void PressCommand(Window window, Key key, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            window.KeyPress(key, Command(window), PhysicalKey.None, null);
            window.KeyRelease(key, Command(window), PhysicalKey.None, null);
        }

        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void StatusBarShowsZoomLevel()
    {
        var window = ShowWindow();

        Assert.Equal("100%", ZoomText(window));
    }

    [AvaloniaFact]
    public void CommandPageUpAndPageDownStepThroughZoomLevels()
    {
        var window = ShowWindow(640, 480);

        PressCommand(window, Key.PageUp);
        Assert.Equal("200%", ZoomText(window));
        Assert.Equal(new Size(1280, 960), RenderedRect(window).Size);

        PressCommand(window, Key.PageDown, times: 2);
        Assert.Equal("50%", ZoomText(window));
        Assert.Equal(new Size(320, 240), RenderedRect(window).Size);
        Assert.Equal(new Size(640, 480), Canvas(window).Bounds.Size); // the canvas itself is untouched
    }

    [AvaloniaFact]
    public void ZoomIsLimitedToPaintRange()
    {
        var window = ShowWindow();

        PressCommand(window, Key.PageUp, times: 20);
        Assert.Equal("800%", ZoomText(window));

        PressCommand(window, Key.PageDown, times: 20);
        Assert.Equal(0.125, Zoom(window).ZoomFactor);
    }

    [AvaloniaFact]
    public void ZoomButtonsChangeZoom()
    {
        var window = ShowWindow(640, 480);

        Click(window, "ZoomInButton");
        Assert.Equal("200%", ZoomText(window));
        Assert.Equal(new Size(1280, 960), RenderedRect(window).Size);

        Click(window, "ZoomOutButton");
        Assert.Equal("100%", ZoomText(window));
    }

    [AvaloniaFact]
    public void CommandWheelOverCanvasZoomsAroundPointer()
    {
        var window = ShowWindow(640, 480);
        var pointer = RenderedRect(window).TopLeft + new Point(100, 50); // document pixel (100, 50)

        window.MouseWheel(pointer, new Vector(0, 1), Command(window));
        window.UpdateLayout();

        Assert.Equal("200%", ZoomText(window));
        var underPointer = window.TranslatePoint(pointer, Canvas(window))!.Value;
        Assert.Equal(100, underPointer.X, 0.5);
        Assert.Equal(50, underPointer.Y, 0.5);

        window.MouseWheel(pointer, new Vector(0, -1), Command(window));
        window.UpdateLayout();
        Assert.Equal("100%", ZoomText(window));
    }

    [AvaloniaFact]
    public void FitToWindowShowsWholeCanvas()
    {
        var window = ShowWindow(4000, 3000);
        var scroller = Scroller(window);

        Click(window, "FitToWindowButton");

        Assert.InRange(Zoom(window).ZoomFactor, 0.125, 1);
        var viewport = new Rect(scroller.TranslatePoint(default, window)!.Value, scroller.Viewport);
        Assert.True(viewport.Contains(RenderedRect(window)), $"{RenderedRect(window)} not within {viewport}");
    }

    [AvaloniaFact]
    public void WheelPansVerticallyAndShiftWheelPansHorizontally()
    {
        var window = ShowWindow(4000, 3000);
        var scroller = Scroller(window);
        var pointer = CentreOf(window, scroller);

        window.MouseWheel(pointer, new Vector(0, -1));
        window.UpdateLayout();
        Assert.True(scroller.Offset.Y > 0);
        Assert.Equal(0, scroller.Offset.X);

        window.MouseWheel(pointer, new Vector(0, -1), RawInputModifiers.Shift);
        window.UpdateLayout();
        Assert.True(scroller.Offset.X > 0);
        Assert.Equal("100%", ZoomText(window));
    }

    [AvaloniaFact]
    public void ScrollbarsAppearWhenZoomedCanvasExceedsView()
    {
        var window = ShowWindow(640, 480);
        var scroller = Scroller(window);
        Assert.Equal(0, scroller.ScrollBarMaximum.X);

        PressCommand(window, Key.PageUp);

        Assert.True(scroller.ScrollBarMaximum.X > 0);
        Assert.True(scroller.ScrollBarMaximum.Y > 0);
    }

    /// <summary>
    /// A black and a white document pixel, zoomed: each must cover exactly <c>zoom</c> device pixels
    /// (100% = one device pixel, also on HiDPI) with no blended edge (nearest-neighbour).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1.0, 8.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(2.0, 4.0)]
    [InlineData(1.5, 2.0)]
    public void ZoomedPixelsStayCrisp(double renderScaling, double zoom)
    {
        var window = ShowWindow(2, 1);
        window.SetRenderScaling(renderScaling);
        Canvas(window).Child = new Image { Source = BlackWhiteBitmap(), Width = 2, Height = 1 };
        Zoom(window).ZoomFactor = zoom;
        window.UpdateLayout();

        var frame = window.CaptureRenderedFrame()!;
        var origin = Canvas(window).TranslatePoint(default, window)!.Value * renderScaling;
        int x0 = (int)Math.Round(origin.X), y = (int)Math.Round(origin.Y);
        var n = (int)zoom;

        Assert.Equal(0x000000, ReadRgb(frame, x0, y));
        Assert.Equal(0x000000, ReadRgb(frame, x0 + n - 1, y + n - 1));
        Assert.Equal(0xFFFFFF, ReadRgb(frame, x0 + n, y));
        Assert.Equal(0xFFFFFF, ReadRgb(frame, x0 + 2 * n - 1, y + n - 1));
    }

    private static WriteableBitmap BlackWhiteBitmap()
    {
        var bitmap = new WriteableBitmap(new PixelSize(2, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        Marshal.Copy(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }, 0, buffer.Address, 8);
        return bitmap;
    }

    private static int ReadRgb(WriteableBitmap frame, int x, int y)
    {
        using var buffer = frame.Lock();
        return Marshal.ReadInt32(buffer.Address, y * buffer.RowBytes + x * 4) & 0xFFFFFF;
    }
}
