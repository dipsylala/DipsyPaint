using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using Point = Avalonia.Point;
using Size = Avalonia.Size;

namespace DipsyPaint.Tests;

public class ClipboardTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    /// <summary>Records what the clipboard commands do, passing them on to <paramref name="inner"/> if given.</summary>
    private sealed class RecordingClipboard(IImageClipboard? inner = null) : IImageClipboard
    {
        public List<PixelImage> Copied { get; } = [];
        public PixelImage? ToPaste { get; set; }
        public List<string> Errors { get; } = [];

        public Task SetImageAsync(PixelImage image)
        {
            Copied.Add(image);
            return inner?.SetImageAsync(image) ?? Task.CompletedTask;
        }

        public Task<PixelImage?> GetImageAsync() => inner?.GetImageAsync() ?? Task.FromResult(ToPaste);

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Shows a white document with a red 20×20 square at (10, 10) and the rectangle selection tool active.</summary>
    private static (MainWindow Window, ClipboardControl Control) ShowWindow(int width = 64, int height = 48)
    {
        var document = Document.CreateBlank(width, height);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        ToolButton(window, SelectionStrings.Select).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return (window, window.GetLogicalDescendants().OfType<ClipboardControl>().Single());
    }

    private static ToggleButton ToolButton(Window window, string name) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name);

    private static Button ClipboardButton(ClipboardControl control, string name) => control.FindControl<Button>(name)!;

    private static IClipboard OsClipboard(Window window) => window.Clipboard!;

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static void SelectRedSquare(Window window) => Drag(window, (10.5, 10.5), (29.5, 29.5));

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static void PressCommand(Window window, Key key)
    {
        var command = (RawInputModifiers)window.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;
        window.KeyPress(key, command, PhysicalKey.None, null);
        window.KeyRelease(key, command, PhysicalKey.None, null);
    }

    private static Rgba LayerPixel(MainWindow window, int x, int y) => window.Document.ActiveLayer.Pixels.GetPixel(x, y);

    private static PixelImage BlueImage(int width, int height) => new(width, height, Enumerable.Repeat(Blue, width * height).ToArray());

    private static async Task PutOnClipboard(Window window, DataFormat<byte[]> format, byte[] bytes)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(format, bytes));
        await OsClipboard(window).SetDataAsync(data);
    }

    /// <summary>A 4×3 blue image encoded the way another app on this OS would put it on the clipboard.</summary>
    private static (DataFormat<byte[]> Format, byte[] Bytes) ForeignImage()
    {
        using var image = new Image<Rgba32>(4, 3, new Rgba32(0, 0, 255, 255));
        using var stream = new MemoryStream();
        string format;
        if (OperatingSystem.IsWindows())
        {
            image.SaveAsBmp(stream);
            return (DataFormat.CreateBytesPlatformFormat("CF_DIB"), stream.ToArray()[14..]);
        }

        if (OperatingSystem.IsMacOS())
        {
            image.SaveAsTiff(stream);
            format = "public.tiff";
        }
        else
        {
            image.SaveAsPng(stream);
            format = "image/png";
        }

        return (DataFormat.CreateBytesPlatformFormat(format), stream.ToArray());
    }

    [AvaloniaFact]
    public void ClipboardGroupHasNamedButtons_AndCutAndCopyNeedASelection()
    {
        var (window, control) = ShowWindow();

        Assert.Equal(ClipboardStrings.Clipboard, AutomationProperties.GetName(control));
        Assert.Equal(ClipboardStrings.Paste, AutomationProperties.GetName(ClipboardButton(control, "PasteButton")));
        Assert.True(ClipboardButton(control, "PasteButton").IsEnabled);
        Assert.False(ClipboardButton(control, "CutButton").IsEnabled);
        Assert.False(ClipboardButton(control, "CopyButton").IsEnabled);

        SelectRedSquare(window);

        Assert.True(ClipboardButton(control, "CutButton").IsEnabled);
        Assert.True(ClipboardButton(control, "CopyButton").IsEnabled);

        window.Editor.CancelPending();

        Assert.False(ClipboardButton(control, "CopyButton").IsEnabled);
    }

    [AvaloniaFact]
    public void ShortcutsMatchReferencePaint()
    {
        var (window, control) = ShowWindow();
        var clipboard = new RecordingClipboard { ToPaste = BlueImage(3, 2) };
        control.ImageClipboard = clipboard;
        SelectRedSquare(window);

        PressCommand(window, Key.C);

        var copied = Assert.Single(clipboard.Copied);
        Assert.Equal((20, 20), (copied.Width, copied.Height));
        Assert.Equal(Red, LayerPixel(window, 10, 10));

        PressCommand(window, Key.X);

        Assert.Equal(2, clipboard.Copied.Count);
        Assert.Null(window.Editor.Selection);
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));

        PressCommand(window, Key.V);

        Assert.Equal(new IntRect(0, 0, 3, 2), window.Editor.Selection?.Bounds);
        Assert.Empty(clipboard.Errors);
    }

    [AvaloniaFact]
    public void CutIsOneUndoableStep()
    {
        var (window, control) = ShowWindow();
        control.ImageClipboard = new RecordingClipboard();
        SelectRedSquare(window);
        Drag(window, (20.5, 20.5), (30.5, 30.5)); // move it first

        PressCommand(window, Key.X);
        window.Editor.Undo();

        Assert.Equal(Red, LayerPixel(window, 10, 10));
        Assert.Equal(Rgba.White, LayerPixel(window, 39, 39));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ShortcutsInAFocusedTextBox_AreLeftToIt()
    {
        var (window, control) = ShowWindow();
        var clipboard = new RecordingClipboard { ToPaste = BlueImage(3, 2) };
        control.ImageClipboard = clipboard;
        var textBox = new TextBox { Text = "text" };
        ((Panel)window.FindControl<Border>("CommandBar")!.Child!).Children.Add(textBox);
        window.UpdateLayout();
        SelectRedSquare(window);
        textBox.Focus();
        textBox.SelectAll();

        PressCommand(window, Key.C);
        PressCommand(window, Key.X);
        PressCommand(window, Key.V);

        Assert.Empty(clipboard.Copied);
        Assert.Equal(new IntRect(10, 10, 20, 20), window.Editor.Selection?.Bounds);
        Assert.Equal(Red, LayerPixel(window, 10, 10));
    }

    [AvaloniaFact]
    public async Task CopyThenPaste_RoundTripsThroughTheClipboard_AsAMovableSelection()
    {
        var (window, control) = ShowWindow();
        var halfGreen = new Rgba(0, 64, 0, 128);
        var pixels = Enumerable.Repeat(Red, 20 * 10).ToArray();
        pixels[0] = halfGreen;
        window.Editor.Paste(new PixelImage(20, 10, pixels), 40, 30); // a lifted selection keeps its alpha

        await control.CopyAsync();
        window.Editor.CancelPending();
        await control.PasteAsync();

        var selection = Assert.IsType<Selection>(window.Editor.Selection);
        Assert.Equal(new IntRect(0, 0, 20, 10), selection.Bounds);
        Assert.Equal(pixels, selection.FloatingPixels);
        Assert.Equal(Rgba.White, LayerPixel(window, 0, 0)); // lifted: nothing written yet

        Drag(window, (10.5, 5), (20.5, 25));                // move it by (10, 20)
        Assert.Equal(new IntRect(10, 20, 20, 10), window.Editor.Selection?.Bounds);
        window.Editor.CommitPending();

        Assert.Equal(halfGreen.Over(Red), LayerPixel(window, 10, 20)); // over the red square
        Assert.Equal(Red, LayerPixel(window, 29, 29));
        Assert.Equal(Rgba.White, LayerPixel(window, 0, 0));
        Assert.Equal(Rgba.White, LayerPixel(window, 40, 30)); // the cancelled original wrote nothing
    }

    [AvaloniaFact]
    public async Task Copy_PutsPngOnTheClipboardForOtherApps()
    {
        var (window, control) = ShowWindow();
        SelectRedSquare(window);

        await control.CopyAsync();

        using var data = await OsClipboard(window).TryGetDataAsync();
        var formats = data!.Formats;
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(DataFormat.Bitmap, formats); // which Avalonia offers as PNG and DIB
        }
        else
        {
            var png = await data.TryGetValueAsync(DataFormat.CreateBytesPlatformFormat(OperatingSystem.IsMacOS() ? "public.png" : "image/png"));
            using var image = Image.Load<Rgba32>(png!);
            Assert.Equal((20, 20), (image.Width, image.Height));
            Assert.Equal(new Rgba32(255, 0, 0, 255), image[0, 0]);
        }
    }

    [AvaloniaFact]
    public async Task AnImageFromAnotherApp_Pastes()
    {
        var (window, control) = ShowWindow();
        var (format, bytes) = ForeignImage();
        await PutOnClipboard(window, format, bytes);

        await control.PasteAsync();

        var selection = Assert.IsType<Selection>(window.Editor.Selection);
        Assert.Equal(new IntRect(0, 0, 4, 3), selection.Bounds);
        Assert.All(selection.FloatingPixels!, p => Assert.Equal(Blue, p));
        Assert.IsType<SelectionTool>(window.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public async Task OnWindows_AnImageOfferedOnlyAsAGdiBitmap_Pastes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows hands over CF_BITMAP-only images as an Avalonia bitmap.");
        var (window, control) = ShowWindow();
        var bitmap = new WriteableBitmap(new PixelSize(4, 3), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
        {
            var bgra = Enumerable.Repeat<byte[]>([255, 0, 0, 0], 12).SelectMany(p => p).ToArray(); // blue, alpha unused
            Marshal.Copy(bgra, 0, buffer.Address, bgra.Length);
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DataFormat.Bitmap, (Bitmap)bitmap));
        await OsClipboard(window).SetDataAsync(data);

        await control.PasteAsync();

        var selection = Assert.IsType<Selection>(window.Editor.Selection);
        Assert.Equal(new IntRect(0, 0, 4, 3), selection.Bounds);
        Assert.All(selection.FloatingPixels!, p => Assert.Equal(Blue, p));
    }

    [AvaloniaFact]
    public async Task DamagedClipboardData_ShowsAMessage_AndPastesNothing()
    {
        var (window, control) = ShowWindow();
        var clipboard = new RecordingClipboard(control.ImageClipboard);
        control.ImageClipboard = clipboard;
        var (format, _) = ForeignImage();
        await PutOnClipboard(window, format, "not an image at all, just some text"u8.ToArray());

        await control.PasteAsync();

        var error = Assert.Single(clipboard.Errors);
        Assert.StartsWith(ClipboardStrings.CouldNotPaste, error);
        Assert.Null(window.Editor.Selection);
    }

    [AvaloniaFact]
    public async Task TextOnTheClipboard_PastesNothing()
    {
        var (window, control) = ShowWindow();
        var clipboard = new RecordingClipboard(control.ImageClipboard);
        control.ImageClipboard = clipboard;
        await OsClipboard(window).SetTextAsync("hello");

        await control.PasteAsync();

        Assert.Empty(clipboard.Errors);
        Assert.Null(window.Editor.Selection);
    }

    [AvaloniaFact]
    public void Paste_GoesToTheTopLeftOfTheVisibleArea_AndSelectsTheSelectionTool()
    {
        var (window, control) = ShowWindow(2000, 1500);
        control.ImageClipboard = new RecordingClipboard { ToPaste = BlueImage(3, 2) };
        ToolButton(window, ToolStrings.Pencil).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<ScrollViewer>("CanvasScroll")!.Offset = new Vector(300, 200);
        window.UpdateLayout();

        PressCommand(window, Key.V);

        Assert.Equal(new IntRect(276, 176, 3, 2), window.Editor.Selection?.Bounds); // the canvas has a 24 px margin
        Assert.True(ToolButton(window, SelectionStrings.Select).IsChecked);
        Assert.False(ToolButton(window, ToolStrings.Pencil).IsChecked);
    }

    [AvaloniaFact]
    public void AnImageLargerThanTheCanvas_EnlargesIt()
    {
        var (window, control) = ShowWindow();
        control.ImageClipboard = new RecordingClipboard { ToPaste = BlueImage(100, 80) };

        PressCommand(window, Key.V);
        window.UpdateLayout();

        Assert.Equal((100, 80), (window.Document.Width, window.Document.Height));
        Assert.Equal(new Size(100, 80), window.FindControl<DocumentCanvas>("DocumentView")!.Bounds.Size);
        Assert.Equal(new IntRect(0, 0, 100, 80), window.Editor.Selection?.Bounds);
    }
}
