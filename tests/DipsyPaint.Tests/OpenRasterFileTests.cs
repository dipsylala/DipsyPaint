using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Files;
using DipsyPaint.Layers;

namespace DipsyPaint.Tests;

/// <summary>Saving and opening OpenRaster project files through the File menu (ticket 18).</summary>
public sealed class OpenRasterFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-ora-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }
        public List<string> Errors { get; } = [];
        public int Warnings { get; private set; }

        public Task<PickedFile?> PickOpenAsync() => Task.FromResult(OpenResult);

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format) => Task.FromResult(SaveResult);

        public Task<(int Width, int Height)?> AskNewImageSizeAsync(int width, int height) => Task.FromResult<(int, int)?>((width, height));

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmLossySaveAsync()
        {
            Warnings++;
            return Task.FromResult(true);
        }
    }

    private sealed class DontSavePrompt : ISaveChangesPrompt
    {
        public Task<SaveChangesChoice> AskAsync(string documentName) => Task.FromResult(SaveChangesChoice.DontSave);
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

    private static (MainWindow Window, FileMenu Menu, FakeDialogs Dialogs) ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(16, 12));
        window.Show();
        window.UpdateLayout();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new DontSavePrompt();
        return (window, menu, dialogs);
    }

    /// <summary>Two layers, the top one hidden and active, each with a dot.</summary>
    private static void DrawTwoLayers(MainWindow window)
    {
        window.Document.ActiveLayer.Pixels.SetPixel(1, 1, new Rgba(255, 0, 0, 255));
        Assert.True(window.Editor.AddLayer());
        var top = window.Document.ActiveLayer;
        top.Pixels.SetPixel(2, 2, new Rgba(0, 0, 255, 255));
        window.Editor.SetLayerVisible(top, false);
    }

    private static Rgba[] PixelsOf(Layer layer)
    {
        var pixels = new Rgba[layer.Pixels.Width * layer.Pixels.Height];
        layer.Pixels.ReadRegion(new IntRect(0, 0, layer.Pixels.Width, layer.Pixels.Height), pixels);
        return pixels;
    }

    [AvaloniaFact]
    public async Task SavingAsOra_KeepsLayers_AndOpeningItRestoresThem()
    {
        var (window, menu, dialogs) = ShowWindow();
        DrawTwoLayers(window);
        var saved = window.Document;
        dialogs.SaveResult = FileOnDisk("drawing.ora");

        Assert.True(await menu.SaveAsAsync());

        Assert.Equal(0, dialogs.Warnings);
        Assert.False(window.Editor.HasUnsavedChanges);
        Assert.Equal("drawing.ora - DipsyPaint", window.Title);

        var (other, otherMenu, otherDialogs) = ShowWindow();
        otherDialogs.OpenResult = FileOnDisk("drawing.ora");
        await otherMenu.OpenAsync();

        Assert.Empty(otherDialogs.Errors);
        var opened = other.Document;
        Assert.Equal(2, opened.Layers.Count);
        Assert.Equal([true, false], opened.Layers.Select(l => l.IsVisible));
        Assert.Equal(PixelsOf(saved.Layers[0]), PixelsOf(opened.Layers[0]));
        Assert.Equal(PixelsOf(saved.Layers[1]), PixelsOf(opened.Layers[1]));
        Assert.Same(opened.Layers[1], opened.ActiveLayer);
        other.UpdateLayout();
        Assert.Equal(2, other.FindControl<LayersPanel>("Layers")!.LayerRows.Count());
        Assert.Equal("drawing.ora - DipsyPaint", other.Title);
    }

    [AvaloniaFact]
    public async Task SaveAfterOpeningAnOra_WritesOraAgain()
    {
        var (window, menu, dialogs) = ShowWindow();
        DrawTwoLayers(window);
        dialogs.SaveResult = FileOnDisk("drawing.ora");
        await menu.SaveAsAsync();
        dialogs.OpenResult = FileOnDisk("drawing.ora");
        await menu.OpenAsync();
        Assert.True(window.Editor.AddLayer());

        Assert.True(await menu.SaveAsync());

        await using var stream = File.OpenRead(Path.Combine(_folder, "drawing.ora"));
        Assert.Equal(3, ImageCodec.Open(stream).Layers.Count);
    }

    [AvaloniaFact]
    public async Task OpeningAMaliciousOra_ShowsCouldNotOpen_AndKeepsTheDocument()
    {
        var (window, menu, dialogs) = ShowWindow();
        var before = window.Document;
        // A zip-slip entry name.
        byte[] malicious;
        using (var stream = new MemoryStream())
        {
            using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var entry = zip.CreateEntry("mimetype").Open())
                {
                    entry.Write("image/openraster"u8);
                }

                zip.CreateEntry("../../evil.png");
            }

            malicious = stream.ToArray();
        }

        dialogs.OpenResult = FileOnDisk("evil.ora", malicious);

        await menu.OpenAsync();

        Assert.Equal("Could not open \"evil.ora\".", dialogs.Errors.Single().Split(Environment.NewLine)[0]);
        Assert.Same(before, window.Document);
        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }
}
