using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Files;

namespace DipsyPaint.Tests;

public sealed class FileMenuTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-files-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }
        public int OpenRequests { get; private set; }
        public List<(string SuggestedName, ImageFormat Format)> SaveRequests { get; } = [];
        public List<string> Errors { get; } = [];

        public Task<PickedFile?> PickOpenAsync()
        {
            OpenRequests++;
            return Task.FromResult(OpenResult);
        }

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format)
        {
            SaveRequests.Add((suggestedName, format));
            return Task.FromResult(SaveResult);
        }

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private PickedFile FileOnDisk(string name, byte[]? content = null)
    {
        var path = Path.Combine(_folder, name);
        if (content is not null)
        {
            File.WriteAllBytes(path, content);
        }

        return new PickedFile(name, () => Task.FromResult<Stream>(File.OpenRead(path)), () => Task.FromResult<Stream>(File.Create(path)));
    }

    private string PathOf(string name) => Path.Combine(_folder, name);

    private Document Reopen(string name)
    {
        using var stream = File.OpenRead(PathOf(name));
        return ImageCodec.Open(stream);
    }

    private static (MainWindow Window, FileMenu Menu, FakeDialogs Dialogs) ShowWindow(int width = 64, int height = 48)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        window.UpdateLayout();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        return (window, menu, dialogs);
    }

    private static byte[] PngOf(Document document)
    {
        using var stream = new MemoryStream();
        ImageCodec.Export(new Compositor(document), stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static void DrawDot(MainWindow window, int x, int y)
    {
        window.Editor.Apply(new PointerInput(PointerAction.Down, x + 0.5, y + 0.5, PointerButton.Left));
        window.Editor.Apply(new PointerInput(PointerAction.Up, x + 0.5, y + 0.5, PointerButton.Left));
    }

    private static string StatusText(Window window) => window.FindControl<TextBlock>("CanvasSizeText")!.Text!;

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static void Press(Window window, Key key, RawInputModifiers modifiers)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void TitleShowsUntitledForANewDocument()
    {
        var (window, _, _) = ShowWindow();

        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }

    [AvaloniaFact]
    public void CommandNReplacesTheDocumentWithABlankDefaultSizedOne()
    {
        var (window, _, _) = ShowWindow(64, 48);
        DrawDot(window, 3, 3);
        Assert.True(window.Editor.History.CanUndo);
        var before = window.Document;

        Press(window, Key.N, Command(window));

        Assert.NotSame(before, window.Document);
        Assert.Equal((Document.DefaultWidth, Document.DefaultHeight), (window.Document.Width, window.Document.Height));
        Assert.Equal(Rgba.White, window.Document.ActiveLayer.Pixels.GetPixel(3, 3));
        Assert.False(window.Editor.History.CanUndo);
        Assert.Equal($"{Document.DefaultWidth} × {Document.DefaultHeight}px", StatusText(window));
        Assert.Equal(new Size(Document.DefaultWidth, Document.DefaultHeight), window.FindControl<DocumentCanvas>("DocumentView")!.Bounds.Size);
        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }

    [AvaloniaFact]
    public async Task OpenShowsTheImageAndUpdatesTitleAndStatusBar()
    {
        var (window, menu, dialogs) = ShowWindow();
        DrawDot(window, 1, 1);
        var image = Document.CreateBlank(40, 30);
        image.ActiveLayer.Pixels.SetPixel(5, 6, new Rgba(0, 0, 255, 255));
        dialogs.OpenResult = FileOnDisk("picture.png", PngOf(image));

        await menu.OpenAsync();
        window.UpdateLayout();

        Assert.Equal((40, 30), (window.Document.Width, window.Document.Height));
        Assert.Equal(new Rgba(0, 0, 255, 255), window.Editor.Compositor.Pixels[6 * 40 + 5]);
        Assert.False(window.Editor.History.CanUndo);
        Assert.Equal("40 × 30px", StatusText(window));
        Assert.Equal(new Size(40, 30), window.FindControl<Border>("Canvas")!.Bounds.Size);
        Assert.Equal("picture.png - DipsyPaint", window.Title);
        Assert.Empty(dialogs.Errors);
    }

    [AvaloniaTheory]
    [InlineData("truncated.png")]
    [InlineData("not-an-image.jpg")]
    public async Task FailedOpenShowsCouldNotOpenAndKeepsTheCurrentDocument(string name)
    {
        var (window, menu, dialogs) = ShowWindow();
        DrawDot(window, 2, 2);
        var before = window.Document;
        var png = PngOf(Document.CreateBlank(20, 20));
        dialogs.OpenResult = FileOnDisk(name, name == "truncated.png" ? png[..(png.Length / 2)] : "hello"u8.ToArray());

        await menu.OpenAsync();

        var error = Assert.Single(dialogs.Errors);
        Assert.StartsWith($"Could not open \"{name}\".", error);
        Assert.Same(before, window.Document);
        Assert.True(window.Editor.History.CanUndo);
        Assert.Equal(Rgba.Black, window.Document.ActiveLayer.Pixels.GetPixel(2, 2));
        Assert.Equal("64 × 48px", StatusText(window));
        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }

    [AvaloniaFact]
    public async Task SaveAsWritesTheCompositeAndThenSaveReusesTheFile()
    {
        var (window, menu, dialogs) = ShowWindow();
        DrawDot(window, 4, 5);
        dialogs.SaveResult = FileOnDisk("drawing.bmp");

        await menu.SaveAsAsync(ImageFormat.Bmp);

        Assert.Equal(("Untitled", ImageFormat.Bmp), Assert.Single(dialogs.SaveRequests));
        Assert.Equal("drawing.bmp - DipsyPaint", window.Title);
        var saved = Reopen("drawing.bmp");
        Assert.Equal(window.Editor.Compositor.Pixels.ToArray(), new Compositor(saved).Pixels.ToArray());
        Assert.Equal(24, BitConverter.ToInt16(File.ReadAllBytes(PathOf("drawing.bmp")), 28));

        DrawDot(window, 10, 10);
        await menu.SaveAsync();

        Assert.Single(dialogs.SaveRequests); // no dialog the second time
        Assert.Empty(dialogs.Errors);
        var resaved = Reopen("drawing.bmp");
        Assert.Equal(Rgba.Black, resaved.ActiveLayer.Pixels.GetPixel(10, 10));
    }

    [AvaloniaFact]
    public async Task SaveWithoutAFileAsksWhereToSaveAsPng()
    {
        var (_, menu, dialogs) = ShowWindow();

        await menu.SaveAsync();

        Assert.Equal(("Untitled", ImageFormat.Png), Assert.Single(dialogs.SaveRequests));
    }

    [AvaloniaFact]
    public void ShortcutsMatchReferencePaint()
    {
        var (window, _, dialogs) = ShowWindow();
        var command = Command(window);

        Press(window, Key.O, command);
        Assert.Equal(1, dialogs.OpenRequests);

        Press(window, Key.S, command);
        Press(window, Key.S, command | RawInputModifiers.Shift);
        Press(window, Key.F12, RawInputModifiers.None);
        Assert.Equal(3, dialogs.SaveRequests.Count);
    }
}
