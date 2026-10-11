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

    private static readonly FilePickerFileType AllPictures = new(FileStrings.AllPicturesFileType)
    {
        Patterns = [.. new[] { Png, Jpeg, Bmp }.SelectMany(t => t.Patterns!)],
        MimeTypes = [.. new[] { Png, Jpeg, Bmp }.SelectMany(t => t.MimeTypes!)],
        AppleUniformTypeIdentifiers = [.. new[] { Png, Jpeg, Bmp }.SelectMany(t => t.AppleUniformTypeIdentifiers!)],
    };

    private TopLevel TopLevel => TopLevel.GetTopLevel(anchor) ?? throw new InvalidOperationException("Not attached to a window.");

    public async Task<PickedFile?> PickOpenAsync()
    {
        var files = await TopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = FileStrings.OpenDialogTitle,
            FileTypeFilter = [AllPictures, Png, Jpeg, Bmp],
        });
        return files.Count == 0 ? null : Wrap(files[0]);
    }

    public async Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format)
    {
        var type = TypeOf(format);
        var file = await TopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = FileStrings.SaveAsDialogTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = ImageCodec.Extensions(format)[0].TrimStart('.'),
            FileTypeChoices = [type, .. new[] { Png, Jpeg, Bmp }.Where(t => t != type)],
            ShowOverwritePrompt = true,
        });
        return file is null ? null : Wrap(file);
    }

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

    private static FilePickerFileType TypeOf(ImageFormat format) => format switch
    {
        ImageFormat.Png => Png,
        ImageFormat.Jpeg => Jpeg,
        _ => Bmp,
    };

    private static FilePickerFileType FileType(string name, ImageFormat format, string mimeType, string appleType) => new(name)
    {
        Patterns = [.. ImageCodec.Extensions(format).Select(e => "*" + e)],
        MimeTypes = [mimeType],
        AppleUniformTypeIdentifiers = [appleType],
    };

    private static PickedFile Wrap(IStorageFile file) => new(file.Name, file.OpenReadAsync, file.OpenWriteAsync);
}
