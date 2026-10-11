using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Files;
using DipsyPaint.Settings;
using DipsyPaint.View;

namespace DipsyPaint.Tests;

/// <summary>The View menu's rulers, gridlines and status bar (ticket 31).</summary>
public sealed class ViewTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-view-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static MainWindow ShowWindow(int width = 64, int height = 48, SettingsStore? settings = null)
    {
        var window = new MainWindow(Document.CreateBlank(width, height), settings);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static ViewMenu Menu(Window window) => window.FindControl<ViewMenu>("ViewMenu")!;

    private static MenuItem Item(Window window, string name) => Menu(window).FindControl<MenuItem>(name)!;

    private static CanvasRulers Rulers(Window window) => window.FindControl<CanvasRulers>("Rulers")!;

    private static Gridlines Grid(Window window) => window.FindControl<Gridlines>("Gridlines")!;

    private static ZoomControl Zoom(Window window) => window.FindControl<ZoomControl>("Zoom")!;

    private static Border Canvas(Window window) => window.FindControl<Border>("Canvas")!;

    private static ScrollViewer Scroller(Window window) => window.FindControl<ScrollViewer>("CanvasScroll")!;

    private static void Choose(Window window, string itemName)
    {
        Item(window, itemName).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
    }

    private static void PressCommand(Window window, Key key)
    {
        var command = (RawInputModifiers)window.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;
        window.KeyPress(key, command, PhysicalKey.None, null);
        window.KeyRelease(key, command, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    private static Point TopLeftOf(Visual visual, Window window) => visual.TranslatePoint(default, window)!.Value;

    private static int ReadRgb(WriteableBitmap frame, double x, double y)
    {
        using var buffer = frame.Lock();
        return Marshal.ReadInt32(buffer.Address, (int)Math.Floor(y) * buffer.RowBytes + (int)Math.Floor(x) * 4) & 0xFFFFFF;
    }

    [AvaloniaFact]
    public void RulersGridlinesAndStatusBarToggleFromTheViewMenu()
    {
        var window = ShowWindow();
        var statusBar = window.FindControl<Border>("StatusBar")!;
        var scrollerAt = TopLeftOf(Scroller(window), window);

        // Paint's defaults: rulers and gridlines off, status bar on.
        Assert.False(Rulers(window).IsShown);
        Assert.False(Grid(window).IsVisible);
        Assert.True(statusBar.IsVisible);
        Assert.False(Item(window, "RulersItem").IsChecked);
        Assert.False(Item(window, "GridlinesItem").IsChecked);
        Assert.True(Item(window, "StatusBarItem").IsChecked);

        Choose(window, "RulersItem");
        Choose(window, "GridlinesItem");
        Choose(window, "StatusBarItem");

        Assert.True(Rulers(window).IsShown);
        Assert.True(Item(window, "RulersItem").IsChecked);
        // The rulers take a band along the top and left of the canvas area.
        Assert.Equal(scrollerAt + new Point(CanvasRulers.Thickness, CanvasRulers.Thickness), TopLeftOf(Scroller(window), window));
        Assert.True(Grid(window).IsVisible);
        Assert.True(Item(window, "GridlinesItem").IsChecked);
        Assert.False(statusBar.IsVisible);
        Assert.False(Item(window, "StatusBarItem").IsChecked);

        Choose(window, "RulersItem");
        Choose(window, "GridlinesItem");
        Choose(window, "StatusBarItem");

        Assert.False(Rulers(window).IsShown);
        Assert.Equal(scrollerAt, TopLeftOf(Scroller(window), window));
        Assert.False(Grid(window).IsVisible);
        Assert.True(statusBar.IsVisible);
    }

    [AvaloniaFact]
    public void CommandRTogglesRulersAndCommandGTogglesGridlines()
    {
        var window = ShowWindow();
        Assert.Equal("Ctrl+R", Item(window, "RulersItem").InputGesture?.ToString());
        Assert.Equal("Ctrl+G", Item(window, "GridlinesItem").InputGesture?.ToString());

        PressCommand(window, Key.R);
        PressCommand(window, Key.G);

        Assert.True(Rulers(window).IsShown);
        Assert.True(Grid(window).IsVisible);

        PressCommand(window, Key.R);
        PressCommand(window, Key.G);

        Assert.False(Rulers(window).IsShown);
        Assert.False(Grid(window).IsVisible);
    }

    [AvaloniaFact]
    public void ViewMenuZoomCommandsZoom()
    {
        var window = ShowWindow();

        Choose(window, "ZoomInItem");
        Assert.Equal(2, Zoom(window).ZoomFactor);
        Choose(window, "ZoomOutItem");
        Choose(window, "ZoomOutItem");
        Assert.Equal(0.5, Zoom(window).ZoomFactor);
        Choose(window, "ActualSizeItem");
        Assert.Equal(1, Zoom(window).ZoomFactor);
        Choose(window, "FitToWindowItem");
        Assert.True(Zoom(window).ZoomFactor > 1);
    }

    [AvaloniaTheory]
    [InlineData(8, 1)]
    [InlineData(4, 2)]
    [InlineData(1, 8)]
    [InlineData(0.5, 16)]
    public void GridlinesFollowTheZoom(double zoom, int spacing)
    {
        var window = ShowWindow(64, 48);
        Zoom(window).ZoomTo(zoom);
        Choose(window, "GridlinesItem");
        var frame = window.CaptureRenderedFrame()!;
        var origin = TopLeftOf(Canvas(window), window);
        var step = spacing * zoom; // on-screen pixels between lines
        var row = origin.Y + step / 2; // between two horizontal lines

        Assert.Equal(spacing, Gridlines.SpacingFor(zoom));
        for (var k = 1; k <= 3; k++)
        {
            // A line along the document pixel edge k × spacing, one screen pixel wide, plain canvas between lines.
            var line = ReadRgb(frame, origin.X + k * step, row);
            Assert.InRange(line & 0xFF, 0x60, 0xE0);
            Assert.Equal(0xFFFFFF, ReadRgb(frame, origin.X + k * step - step / 2, row));
            if (step >= 2)
            {
                Assert.Equal(0xFFFFFF, ReadRgb(frame, origin.X + k * step + 1, row));
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(1, 150)]
    [InlineData(2, 333)]
    public void RulerMarksFollowTheZoomAndScrolling(double zoom, double scroll)
    {
        var window = ShowWindow(2000, 2000);
        Choose(window, "RulersItem");
        Zoom(window).ZoomTo(zoom);
        Scroller(window).Offset = new Vector(scroll, scroll);
        window.UpdateLayout();
        var frame = window.CaptureRenderedFrame()!;
        var rulers = TopLeftOf(Rulers(window), window);
        var canvas = TopLeftOf(Canvas(window), window);
        var (major, minor) = CanvasRulers.StepsFor(zoom);
        var inner = CanvasRulers.Thickness - 2; // every mark reaches the ruler's inner edge
        var background = ReadRgb(frame, rulers.X + 5, rulers.Y + 5); // the corner where the rulers meet has no marks

        Assert.True(major * zoom >= 50);
        var first = (int)Math.Ceiling(scroll / zoom / minor) * minor + minor; // the first marks wholly in view
        for (var value = first; value < first + 2 * major; value += minor)
        {
            // Horizontal ruler: a mark at the document x, background halfway to the next.
            Assert.NotEqual(background, ReadRgb(frame, canvas.X + value * zoom, rulers.Y + inner));
            Assert.Equal(background, ReadRgb(frame, canvas.X + (value + minor / 2.0) * zoom, rulers.Y + inner));
            // Vertical ruler: the same for the document y.
            Assert.NotEqual(background, ReadRgb(frame, rulers.X + inner, canvas.Y + value * zoom));
            Assert.Equal(background, ReadRgb(frame, rulers.X + inner, canvas.Y + (value + minor / 2.0) * zoom));
        }
    }

    [Theory]
    [InlineData(0.125, 500, 50)]
    [InlineData(1, 50, 5)]
    [InlineData(2, 50, 5)]
    [InlineData(4, 20, 2)]
    [InlineData(8, 10, 1)]
    [InlineData(0.5, 100, 10)]
    public void RulerStepsKeepMarksReadableAtEveryZoom(double scale, int major, int minor) =>
        Assert.Equal((major, minor), CanvasRulers.StepsFor(scale));

    [AvaloniaFact]
    public async Task RulersAndGridlinesAreNeverSavedIntoTheImage()
    {
        var window = ShowWindow(16, 12);
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new DontSavePrompt();
        window.Editor.ActiveTool = new PencilTool(new StrokeSettings(size: 1));
        window.Editor.Apply(new PointerInput(PointerAction.Down, 3.5, 4.5));
        window.Editor.Apply(new PointerInput(PointerAction.Up, 3.5, 4.5));
        var expected = PixelsOf(window.Document);
        Zoom(window).ZoomTo(8);
        Choose(window, "RulersItem");
        Choose(window, "GridlinesItem");
        window.CaptureRenderedFrame(); // the rulers and gridlines are drawn

        foreach (var name in new[] { "picture.png", "picture.bmp", "picture.ora" })
        {
            var path = Path.Combine(_folder, name);
            dialogs.SaveResult = new PickedFile(name, () => Task.FromResult<Stream>(File.OpenRead(path)), () => Task.FromResult<Stream>(File.Create(path)));
            Assert.True(await menu.SaveAsAsync());

            await using var stream = File.OpenRead(path);
            var reopened = name.EndsWith(".ora", StringComparison.Ordinal) ? OpenRasterCodec.Open(stream) : ImageCodec.Open(stream);
            Assert.Equal(expected, PixelsOf(reopened));
        }

        // Nor do they reach the document itself.
        Assert.Equal(expected, PixelsOf(window.Document));
        Assert.Single(window.Document.Layers);
    }

    [AvaloniaFact]
    public void DisplayOptionsAreSavedAndRestoredAtStart()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var first = ShowWindow(settings: store);
        Assert.False(File.Exists(store.Path)); // nothing written until something changes

        Choose(first, "RulersItem");
        Choose(first, "GridlinesItem");
        Choose(first, "StatusBarItem");
        first.Close();

        var saved = store.Load().View!;
        Assert.True(saved.Rulers);
        Assert.True(saved.Gridlines);
        Assert.False(saved.StatusBar);

        var second = ShowWindow(settings: store);
        Assert.True(Rulers(second).IsShown);
        Assert.True(Grid(second).IsVisible);
        Assert.False(second.FindControl<Border>("StatusBar")!.IsVisible);
        Assert.True(Item(second, "RulersItem").IsChecked);
        Assert.False(Item(second, "StatusBarItem").IsChecked);
    }

    private static Rgba[] PixelsOf(Document document)
    {
        var pixels = document.ActiveLayer.Pixels;
        var result = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), result);
        return result;
    }

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? SaveResult { get; set; }

        public Task<PickedFile?> PickOpenAsync() => Task.FromResult<PickedFile?>(null);

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format) => Task.FromResult(SaveResult);

        public Task<(int Width, int Height, Resolution Resolution)?> AskNewImageSizeAsync(int width, int height) => Task.FromResult<(int, int, Resolution)?>((width, height, Resolution.Default));

        public Task ShowErrorAsync(string message) => throw new InvalidOperationException(message);

        public Task<bool> ConfirmLossySaveAsync() => Task.FromResult(true);
    }

    private sealed class DontSavePrompt : ISaveChangesPrompt
    {
        public Task<SaveChangesChoice> AskAsync(string documentName) => Task.FromResult(SaveChangesChoice.DontSave);
    }
}
