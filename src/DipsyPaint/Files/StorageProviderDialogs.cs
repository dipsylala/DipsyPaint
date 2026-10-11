using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>The platform's native file dialogs (Avalonia storage provider) and a simple message box.</summary>
internal sealed class StorageProviderDialogs(Visual anchor) : IFileDialogs
{
    private static readonly FilePickerFileType Png = FileType(FileStrings.PngFileType, ImageFormat.Png, "image/png", "public.png");
    private static readonly FilePickerFileType Jpeg = FileType(FileStrings.JpegFileType, ImageFormat.Jpeg, "image/jpeg", "public.jpeg");
    private static readonly FilePickerFileType Bmp = FileType(FileStrings.BmpFileType, ImageFormat.Bmp, "image/bmp", "com.microsoft.bmp");
    private static readonly FilePickerFileType Bmp256 = FileType(FormatStrings.Bmp256FileType, ImageFormat.Bmp256, "image/bmp", "com.microsoft.bmp");
    private static readonly FilePickerFileType Bmp16 = FileType(FormatStrings.Bmp16FileType, ImageFormat.Bmp16, "image/bmp", "com.microsoft.bmp");
    private static readonly FilePickerFileType BmpMonochrome = FileType(FormatStrings.BmpMonochromeFileType, ImageFormat.BmpMonochrome, "image/bmp", "com.microsoft.bmp");
    private static readonly FilePickerFileType Gif = FileType(FormatStrings.GifFileType, ImageFormat.Gif, "image/gif", "com.compuserve.gif");
    private static readonly FilePickerFileType Tiff = FileType(FormatStrings.TiffFileType, ImageFormat.Tiff, "image/tiff", "public.tiff");
    private static readonly FilePickerFileType Ico = FileType(FormatStrings.IcoFileType, ImageFormat.Ico, "image/x-icon", "com.microsoft.ico");
    private static readonly FilePickerFileType Webp = FileType(FormatStrings.WebpFileType, ImageFormat.Webp, "image/webp", "org.webmproject.webp");

    // OpenRaster has no system-declared Apple type identifier; macOS matches it by extension.
    private static readonly FilePickerFileType OpenRaster = new(OpenRasterStrings.OpenRasterFileType)
    {
        Patterns = ["*.ora"],
        MimeTypes = ["image/openraster"],
        AppleUniformTypeIdentifiers = [],
    };

    // Reference Paint's order (ADR-0019), then the project format last, where Paint lists .paint.
    // Opening needs only one BMP entry.
    private static readonly FilePickerFileType[] OpenTypes = [Bmp, Jpeg, Gif, Tiff, Png, Ico, Webp, OpenRaster];

    private static readonly (ImageFormat Format, FilePickerFileType Type)[] SaveTypes =
    [
        (ImageFormat.BmpMonochrome, BmpMonochrome), (ImageFormat.Bmp16, Bmp16), (ImageFormat.Bmp256, Bmp256),
        (ImageFormat.Bmp, Bmp), (ImageFormat.Jpeg, Jpeg), (ImageFormat.Gif, Gif), (ImageFormat.Tiff, Tiff), (ImageFormat.Png, Png),
        (ImageFormat.OpenRaster, OpenRaster),
    ];

    private static readonly FilePickerFileType AllPictures = new(FileStrings.AllPicturesFileType)
    {
        Patterns = [.. OpenTypes.SelectMany(t => t.Patterns!)],
        MimeTypes = [.. OpenTypes.SelectMany(t => t.MimeTypes!)],
        AppleUniformTypeIdentifiers = [.. OpenTypes.SelectMany(t => t.AppleUniformTypeIdentifiers!)],
    };

    private TopLevel TopLevel => TopLevel.GetTopLevel(anchor) ?? throw new InvalidOperationException("Not attached to a window.");

    public async Task<PickedFile?> PickOpenAsync()
    {
        var files = await TopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = FileStrings.OpenDialogTitle,
            FileTypeFilter = [AllPictures, .. OpenTypes],
        });
        return files.Count == 0 ? null : Wrap(files[0]);
    }

    public async Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format)
    {
        var type = TypeOf(format);
        var result = await TopLevel.StorageProvider.SaveFilePickerWithResultAsync(new FilePickerSaveOptions
        {
            Title = FileStrings.SaveAsDialogTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = ImageCodec.Extensions(format)[0].TrimStart('.'),
            FileTypeChoices = [type, .. SaveTypes.Select(s => s.Type).Where(t => t != type)],
            ShowOverwritePrompt = true,
        });
        // Matched by name: the platform may hand back a copy. Null when the platform doesn't say.
        var chosen = SaveTypes.Where(s => s.Type.Name == result.SelectedFileType?.Name).Select(s => (ImageFormat?)s.Format).FirstOrDefault();
        return result.File is { } file ? Wrap(file) with { Format = chosen } : null;
    }

    public async Task<bool> ConfirmLossySaveAsync() =>
        TopLevel is Window owner && await new LossySaveWarning().ShowDialog<bool>(owner);

    public async Task<(int Width, int Height)?> AskNewImageSizeAsync(int width, int height) =>
        TopLevel is Window owner
            ? await new NewImageDialog(width, height).ShowDialog<(int Width, int Height)?>(owner)
            : null;

    public Task ShowErrorAsync(string message)
    {
        var ok = new Button { Content = FileStrings.Ok, IsDefault = true, IsCancel = true, MinWidth = 80, HorizontalAlignment = HorizontalAlignment.Right };
        AutomationProperties.SetName(ok, FileStrings.Ok);
        var dialog = new Window
        {
            Title = Strings.AppName,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                MaxWidth = 420,
                Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, ok },
            },
        };
        ok.Click += (_, _) => dialog.Close();
        return TopLevel is Window owner ? dialog.ShowDialog(owner) : Task.CompletedTask;
    }

    private static FilePickerFileType TypeOf(ImageFormat format) =>
        SaveTypes.FirstOrDefault(s => s.Format == format).Type ?? Png;

    private static FilePickerFileType FileType(string name, ImageFormat format, string mimeType, string appleType) => new(name)
    {
        Patterns = [.. ImageCodec.Extensions(format).Select(e => "*" + e)],
        MimeTypes = [mimeType],
        AppleUniformTypeIdentifiers = [appleType],
    };

    private static PickedFile Wrap(IStorageFile file) => new(file.Name, file.OpenReadAsync, file.OpenWriteAsync);
}
