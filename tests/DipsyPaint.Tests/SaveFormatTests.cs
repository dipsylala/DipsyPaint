using System.Buffers.Binary;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Files;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

/// <summary>GIF, TIFF, the low-colour bitmaps and the warning before a lossy save (ticket 25).</summary>
public sealed class SaveFormatTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-formats-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }
        public List<ImageFormat> SaveRequests { get; } = [];
        public List<string> Errors { get; } = [];
        public int Warnings { get; private set; }

        /// <summary>Whether the user presses OK on the lossy-save warning.</summary>
        public bool AcceptWarning { get; set; } = true;

        public Task<PickedFile?> PickOpenAsync() => Task.FromResult(OpenResult);

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format)
        {
            SaveRequests.Add(format);
            return Task.FromResult(SaveResult);
        }

        public Task<(int Width, int Height)?> AskNewImageSizeAsync(int width, int height) => Task.FromResult<(int, int)?>((width, height));

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmLossySaveAsync()
        {
            Warnings++;
            return Task.FromResult(AcceptWarning);
        }
    }

    private PickedFile FileOnDisk(string name, byte[]? content = null, ImageFormat? chosen = null)
    {
        var path = PathOf(name);
        if (content is not null)
        {
            File.WriteAllBytes(path, content);
        }

        return new PickedFile(name, () => Task.FromResult<Stream>(File.OpenRead(path)), () => Task.FromResult<Stream>(File.Create(path)), chosen);
    }

    private string PathOf(string name) => Path.Combine(_folder, name);

    private static short BmpBitsPerPixel(byte[] file) => BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(28));

    private static (MainWindow Window, FileMenu Menu, FakeDialogs Dialogs) ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        window.Editor.Apply(new PointerInput(PointerAction.Down, 4.5, 4.5, PointerButton.Left));
        window.Editor.Apply(new PointerInput(PointerAction.Up, 4.5, 4.5, PointerButton.Left));
        return (window, menu, dialogs);
    }

    [AvaloniaTheory]
    [InlineData(ImageFormat.Gif)]
    [InlineData(ImageFormat.Bmp256)]
    [InlineData(ImageFormat.Bmp16)]
    [InlineData(ImageFormat.BmpMonochrome)]
    public async Task SavingToAPaletteFormatWarnsFirst(ImageFormat format)
    {
        var (window, menu, dialogs) = ShowWindow();
        var name = format == ImageFormat.Gif ? "drawing.gif" : "drawing.bmp";
        dialogs.SaveResult = FileOnDisk(name);

        await menu.SaveAsAsync(format);

        Assert.Equal(1, dialogs.Warnings);
        Assert.True(File.Exists(PathOf(name)));
        Assert.Equal($"{name} - DipsyPaint", window.Title);
        Assert.Empty(dialogs.Errors);
    }

    [AvaloniaTheory]
    [InlineData(ImageFormat.Png, "a.png")]
    [InlineData(ImageFormat.Jpeg, "a.jpg")]
    [InlineData(ImageFormat.Bmp, "a.bmp")]
    [InlineData(ImageFormat.Tiff, "a.tif")]
    public async Task SavingToAFullColourFormatDoesNotWarn(ImageFormat format, string name)
    {
        var (_, menu, dialogs) = ShowWindow();
        dialogs.SaveResult = FileOnDisk(name);

        await menu.SaveAsAsync(format);

        Assert.Equal(0, dialogs.Warnings);
        Assert.Equal(new Rgba(0, 0, 0, 255), Reopen(name).ActiveLayer.Pixels.GetPixel(4, 4));
    }

    [AvaloniaFact]
    public async Task CancellingTheWarningSavesNothing()
    {
        var (window, menu, dialogs) = ShowWindow();
        dialogs.SaveResult = FileOnDisk("drawing.gif");
        dialogs.AcceptWarning = false;

        await menu.SaveAsAsync(ImageFormat.Gif);

        Assert.Equal(1, dialogs.Warnings);
        Assert.False(File.Exists(PathOf("drawing.gif")));
        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }

    [AvaloniaFact]
    public async Task TheTypeChosenInTheDialogIsUsedAndSaveKeepsIt()
    {
        var (_, menu, dialogs) = ShowWindow();
        dialogs.SaveResult = FileOnDisk("drawing.bmp", chosen: ImageFormat.Bmp256);

        await menu.SaveAsAsync(ImageFormat.Png);

        Assert.Equal(8, BmpBitsPerPixel(File.ReadAllBytes(PathOf("drawing.bmp"))));

        File.Delete(PathOf("drawing.bmp"));
        await menu.SaveAsync();

        Assert.Single(dialogs.SaveRequests); // no dialog the second time
        Assert.Equal(8, BmpBitsPerPixel(File.ReadAllBytes(PathOf("drawing.bmp"))));
        Assert.Equal(2, dialogs.Warnings); // Paint warns on every lossy save
    }

    [AvaloniaFact]
    public async Task TheOfferedTypeIsUsedWhenTheDialogDoesNotReportOne()
    {
        var (_, menu, dialogs) = ShowWindow();
        dialogs.SaveResult = FileOnDisk("drawing.bmp");

        await menu.SaveAsAsync(ImageFormat.BmpMonochrome);

        Assert.Equal(ImageFormat.BmpMonochrome, Assert.Single(dialogs.SaveRequests));
        Assert.Equal(1, BmpBitsPerPixel(File.ReadAllBytes(PathOf("drawing.bmp"))));
    }

    [AvaloniaTheory]
    [InlineData("picture.webp")]
    [InlineData("picture.ico")]
    public async Task AnOpenOnlyFileIsSavedThroughSaveAsOfferingPng(string name)
    {
        var (window, menu, dialogs) = ShowWindow();
        dialogs.OpenResult = FileOnDisk(name, File.ReadAllBytes(SaveTo("source.png", ImageFormat.Png, window)));
        await menu.OpenAsync();
        Assert.Equal($"{name} - DipsyPaint", window.Title);

        await menu.SaveAsync();

        Assert.Equal(ImageFormat.Png, Assert.Single(dialogs.SaveRequests));
    }

    [AvaloniaFact]
    public async Task OpenReadsGifAndTiff()
    {
        var (window, menu, dialogs) = ShowWindow();
        foreach (var format in new[] { ImageFormat.Gif, ImageFormat.Tiff })
        {
            var source = SaveTo($"source{ImageCodec.Extensions(format)[0]}", format, window);
            dialogs.OpenResult = FileOnDisk(Path.GetFileName(source));

            await menu.OpenAsync();

            Assert.Empty(dialogs.Errors);
            Assert.Equal(new Rgba(0, 0, 0, 255), window.Document.ActiveLayer.Pixels.GetPixel(4, 4));
        }
    }

    [AvaloniaFact]
    public void SaveAsMenuOffersGifAndOtherFormats()
    {
        var (window, _, _) = ShowWindow();

        var gif = window.FindControl<FileMenu>("FileMenu")!.FindControl<MenuItem>("SaveAsGifItem")!;
        var other = window.FindControl<FileMenu>("FileMenu")!.FindControl<MenuItem>("SaveAsOtherItem")!;

        Assert.Equal("GIF image", AutomationProperties.GetName(gif));
        Assert.Equal("Other formats", AutomationProperties.GetName(other));
    }

    [AvaloniaFact]
    public void OtherFormatsOpensSaveAsOfferingTheCurrentFormat()
    {
        var (window, _, dialogs) = ShowWindow();
        var other = window.FindControl<FileMenu>("FileMenu")!.FindControl<MenuItem>("SaveAsOtherItem")!;

        other.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(ImageFormat.Png, Assert.Single(dialogs.SaveRequests));
    }

    [AvaloniaFact]
    public void LossySaveWarning_UsesPaintsWordingAndReturnsTheChoice()
    {
        var ok = new LossySaveWarning();
        ok.Show();
        Assert.Equal(
            "Saving into this format may cause some loss of colour information. Do you want to continue?",
            ok.FindControl<TextBlock>("MessageText")!.Text);
        Assert.Equal(FormatStrings.LossySaveWarning, ok.FindControl<TextBlock>("MessageText")!.Text);

        ok.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(ok.Result);
        Assert.False(ok.IsVisible);

        var cancel = new LossySaveWarning();
        cancel.Show();
        cancel.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(cancel.Result);
        Assert.False(cancel.IsVisible);
    }

    private Document Reopen(string name)
    {
        using var stream = File.OpenRead(PathOf(name));
        return ImageCodec.Open(stream);
    }

    // Exports the window's composite straight through the engine, bypassing the menu.
    private string SaveTo(string name, ImageFormat format, MainWindow window)
    {
        using (var stream = File.Create(PathOf(name)))
        {
            ImageCodec.Export(window.Editor.Compositor, stream, format);
        }

        return PathOf(name);
    }
}
