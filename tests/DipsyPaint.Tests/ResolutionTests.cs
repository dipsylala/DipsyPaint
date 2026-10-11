using System.Buffers.Binary;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Files;

namespace DipsyPaint.Tests;

/// <summary>The status bar shows the document's resolution beside the canvas size (ADR-0024).</summary>
public sealed class ResolutionTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-resolution-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }

        public Task<(int Width, int Height)?> AskNewImageSizeAsync(int width, int height) => Task.FromResult<(int, int)?>((width, height));

        public Task<PickedFile?> PickOpenAsync() => Task.FromResult(OpenResult);

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format) => Task.FromResult(SaveResult);

        public Task ShowErrorAsync(string message) => throw new InvalidOperationException(message);

        public Task<bool> ConfirmLossySaveAsync() => Task.FromResult(true);
    }

    private sealed class DontSavePrompt : ISaveChangesPrompt
    {
        public Task<SaveChangesChoice> AskAsync(string documentName) => Task.FromResult(SaveChangesChoice.DontSave);
    }

    private static string StatusText(Window window) => window.FindControl<TextBlock>("CanvasSizeText")!.Text!;

    private static MainWindow ShowWindow(Document document)
    {
        var window = new MainWindow(document);
        window.Show();
        return window;
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

    private static byte[] Export(Document document, ImageFormat format)
    {
        using var stream = new MemoryStream();
        ImageCodec.Export(new Compositor(document), stream, format);
        return stream.ToArray();
    }

    /// <summary>A BMP whose header states a negative resolution.</summary>
    private static byte[] BmpWithNegativeResolution()
    {
        var file = Export(Document.CreateBlank(20, 10), ImageFormat.Bmp);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(38), -3780);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(42), -3780);
        return file;
    }

    [AvaloniaFact]
    public void StatusBar_ShowsOneNumber_WhenBothDirectionsMatch()
    {
        var window = ShowWindow(Document.CreateBlank(2480, 3508, new Resolution(300, 300)));

        Assert.Equal("2480 × 3508 px · 300 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public void StatusBar_ShowsBothDirections_WhenTheyDiffer()
    {
        var window = ShowWindow(Document.CreateBlank(40, 30, new Resolution(204, 196)));

        Assert.Equal("40 × 30 px · 204 × 196 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public void StatusBar_ShowsDpi_ForResolutionsStoredInOtherUnits()
    {
        // 11811 pixels per metre is 299.9994 dpi.
        var window = ShowWindow(Document.CreateBlank(40, 30, new Resolution(11811, 7874, ResolutionUnit.PixelsPerMetre)));

        Assert.Equal("40 × 30 px · 300 × 200 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public void StatusBar_KeepsTheResolution_WhenTheCanvasIsResized()
    {
        var window = ShowWindow(Document.CreateBlank(40, 30, new Resolution(300, 300)));

        window.Editor.Resize(80, 60);

        Assert.Equal("80 × 60 px · 300 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public async Task InvalidFileResolution_IsReportedUntilTheNextSave()
    {
        var window = ShowWindow(Document.CreateBlank(64, 48));
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs { OpenResult = FileOnDisk("bad.bmp", BmpWithNegativeResolution()) };
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new DontSavePrompt();

        await menu.OpenAsync();

        Assert.Equal("20 × 10 px · 96 dpi (file's value was invalid)", StatusText(window));
        window.Editor.Resize(40, 20);
        Assert.Equal("40 × 20 px · 96 dpi (file's value was invalid)", StatusText(window));

        await menu.SaveAsync();

        Assert.Equal("40 × 20 px · 96 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public async Task InvalidFileResolution_IsReportedUntilTheNextOpen()
    {
        var window = ShowWindow(Document.CreateBlank(64, 48));
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs { OpenResult = FileOnDisk("bad.bmp", BmpWithNegativeResolution()) };
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new DontSavePrompt();
        await menu.OpenAsync();
        Assert.EndsWith("(file's value was invalid)", StatusText(window));

        dialogs.OpenResult = FileOnDisk("good.png", Export(Document.CreateBlank(20, 10, new Resolution(150, 150)), ImageFormat.Png));
        await menu.OpenAsync();

        Assert.Equal("20 × 10 px · 150 dpi", StatusText(window));
    }

    [AvaloniaFact]
    public async Task Save_WritesTheOpenedFilesResolution()
    {
        var window = ShowWindow(Document.CreateBlank(64, 48));
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs
        {
            OpenResult = FileOnDisk("scan.jpg", Export(Document.CreateBlank(20, 10, new Resolution(600, 300)), ImageFormat.Jpeg)),
            SaveResult = FileOnDisk("scan.tif"),
        };
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new DontSavePrompt();
        await menu.OpenAsync();

        await menu.SaveAsAsync(ImageFormat.Tiff);

        using var saved = File.OpenRead(Path.Combine(_folder, "scan.tif"));
        Assert.Equal(new Resolution(600, 300), ImageCodec.Open(saved).Resolution);
    }
}
