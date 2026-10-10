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
using DipsyPaint.Images;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class ImageTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private sealed class FakeDialogs : IImageDialogs
    {
        public (int Width, int Height)? ResizeResult { get; set; }
        public List<(int Width, int Height)> ResizeRequests { get; } = [];
        public List<string> Errors { get; } = [];

        public Task<(int Width, int Height)?> AskResizeAsync(int width, int height)
        {
            ResizeRequests.Add((width, height));
            return Task.FromResult(ResizeResult);
        }

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Shows a 64×48 white document with a red pixel at (0, 0).</summary>
    private static (MainWindow Window, ImageControl Image, FakeDialogs Dialogs) ShowWindow()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.SetPixel(0, 0, Red);
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        var image = window.FindControl<ImageControl>("ImageGroup")!;
        var dialogs = new FakeDialogs();
        image.Dialogs = dialogs;
        return (window, image, dialogs);
    }

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static void Press(Window window, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Select(MainWindow window, (double X, double Y) from, (double X, double Y) to)
    {
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == SelectionStrings.RectangleSelection)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static string StatusText(Window window) => window.FindControl<TextBlock>("CanvasSizeText")!.Text!;

    private static void AssertCanvasSize(MainWindow window, int width, int height)
    {
        window.UpdateLayout();
        Assert.Equal((width, height), (window.Document.Width, window.Document.Height));
        Assert.Equal(string.Format(Strings.CanvasSizeFormat, width, height), StatusText(window));
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        Assert.Equal(new Size(width, height), canvas.Bounds.Size);
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
    public void ImageGroup_HasNamedCommands_ThatAreKeyboardReachable()
    {
        var (_, image, _) = ShowWindow();

        string[] buttons = [ImageStrings.Crop, ImageStrings.Resize, ImageStrings.Rotate];
        Assert.All(buttons, name =>
        {
            var button = image.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
            Assert.True(button.Focusable && button.IsTabStop);
        });
        string[] items = ["RotateRightItem", "RotateLeftItem", "Rotate180Item", "FlipVerticalItem", "FlipHorizontalItem"];
        Assert.All(items, name => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(image.FindControl<MenuItem>(name)!))));
    }

    [AvaloniaFact]
    public void RotateRight_RotatesThePicture_AndUpdatesTheCanvasAndStatusBar_AndUndoRestoresThem()
    {
        var (window, image, _) = ShowWindow();

        image.FindControl<MenuItem>("RotateRightItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        AssertCanvasSize(window, 48, 64);
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(47, 0));
        Assert.Equal((255, 0, 0), RenderedPixel(window, InWindow(window, 47.5, 0.5)));

        window.Editor.Undo();

        AssertCanvasSize(window, 64, 48);
        Assert.Equal((255, 0, 0), RenderedPixel(window, InWindow(window, 0.5, 0.5)));
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 47.5, 0.5)));
    }

    [AvaloniaFact]
    public void FlipItems_FlipThePicture()
    {
        var (window, image, _) = ShowWindow();

        image.FindControl<MenuItem>("FlipHorizontalItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        image.FindControl<MenuItem>("FlipVerticalItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(63, 47));
        image.FindControl<MenuItem>("Rotate180Item")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(0, 0));
    }

    [AvaloniaFact]
    public void CommandW_AsksForANewSize_AndResizesThePicture_AndUndoRestoresIt()
    {
        var (window, _, dialogs) = ShowWindow();
        dialogs.ResizeResult = (128, 96);

        Press(window, Key.W, PhysicalKey.W, Command(window));

        Assert.Equal([(64, 48)], dialogs.ResizeRequests);
        AssertCanvasSize(window, 128, 96);
        Assert.Empty(dialogs.Errors);

        Press(window, Key.Z, PhysicalKey.Z, Command(window));

        AssertCanvasSize(window, 64, 48);
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(0, 0));

        Press(window, Key.Y, PhysicalKey.Y, Command(window));

        AssertCanvasSize(window, 128, 96);
    }

    [AvaloniaFact]
    public void Resize_Cancelled_ChangesNothing()
    {
        var (window, _, dialogs) = ShowWindow();

        window.FindControl<ImageControl>("ImageGroup")!.GetLogicalDescendants().OfType<Button>()
            .Single(b => b.Name == "ResizeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Single(dialogs.ResizeRequests);
        AssertCanvasSize(window, 64, 48);
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void Resize_BeyondTheMaximumSide_IsRefusedWithAMessage()
    {
        var (window, image, dialogs) = ShowWindow();
        dialogs.ResizeResult = (Document.MaxSide + 1, 100);

        image.ResizeAsync().GetAwaiter().GetResult();

        var error = Assert.Single(dialogs.Errors);
        Assert.Contains(Document.MaxSide.ToString(), error);
        AssertCanvasSize(window, 64, 48);
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void WithASelection_ResizeOffersTheSelectionsSize_AndResizesOnlyIt()
    {
        var (window, image, dialogs) = ShowWindow();
        Select(window, (10.5, 10.5), (29.5, 19.5)); // 20×10
        dialogs.ResizeResult = (40, 20);

        image.ResizeAsync().GetAwaiter().GetResult();

        Assert.Equal([(20, 10)], dialogs.ResizeRequests);
        Assert.Equal(new IntRect(10, 10, 40, 20), window.Editor.Selection?.Bounds);
        AssertCanvasSize(window, 64, 48);
    }

    [AvaloniaFact]
    public void CommandShiftX_CropsToTheSelection()
    {
        var (window, _, _) = ShowWindow();
        Select(window, (0.5, 0.5), (19.5, 9.5)); // 20×10 from the top left

        Press(window, Key.X, PhysicalKey.X, Command(window) | RawInputModifiers.Shift);

        AssertCanvasSize(window, 20, 10);
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Null(window.Editor.Selection);
    }

    [AvaloniaFact]
    public void ResizeDialog_ByPercentage_KeepsTheAspectRatio_AndReturnsPixels()
    {
        var dialog = new ResizeDialog(64, 48);
        dialog.Show();
        var horizontal = dialog.FindControl<NumericUpDown>("HorizontalBox")!;
        var vertical = dialog.FindControl<NumericUpDown>("VerticalBox")!;
        Assert.Equal((100m, 100m), (horizontal.Value, vertical.Value));

        horizontal.Value = 50;

        Assert.Equal(50m, vertical.Value);
        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((32, 24), dialog.Result);
    }

    [AvaloniaFact]
    public void ResizeDialog_ByPixels_KeepsTheAspectRatio_UnlessUnticked()
    {
        var dialog = new ResizeDialog(64, 48);
        dialog.Show();
        var horizontal = dialog.FindControl<NumericUpDown>("HorizontalBox")!;
        var vertical = dialog.FindControl<NumericUpDown>("VerticalBox")!;

        dialog.FindControl<RadioButton>("PixelsOption")!.IsChecked = true;

        Assert.Equal((64m, 48m), (horizontal.Value, vertical.Value));
        horizontal.Value = 128;
        Assert.Equal(96m, vertical.Value);

        dialog.FindControl<CheckBox>("MaintainAspectRatioBox")!.IsChecked = false;
        vertical.Value = 10;
        Assert.Equal(128m, horizontal.Value);

        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((128, 10), dialog.Result);
    }

    [AvaloniaFact]
    public void ResizeDialog_Cancel_ReturnsNothing()
    {
        var dialog = new ResizeDialog(64, 48);
        dialog.Show();

        dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(dialog.Result);
        Assert.False(dialog.IsVisible);
    }
}
