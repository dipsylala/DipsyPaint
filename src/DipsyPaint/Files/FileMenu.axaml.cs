using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>
/// Reference Paint's File menu: New, Open, Save and Save as (PNG, JPEG, BMP). Owns the current file,
/// shows its name in the window title ("name - DipsyPaint") and handles the File shortcuts for the
/// whole window: Ctrl+N, Ctrl+O, Ctrl+S, and Ctrl+Shift+S or F12 for Save as (Cmd on macOS).
/// </summary>
public partial class FileMenu : UserControl
{
    private MainWindow? _window;
    private PickedFile? _file;

    public FileMenu()
    {
        InitializeComponent();
        Dialogs = new StorageProviderDialogs(this);
    }

    /// <summary>The dialogs to use; tests replace these to bypass native dialogs.</summary>
    public IFileDialogs Dialogs { get; set; }

    private MainWindow Window => _window ?? throw new InvalidOperationException("The File menu is not in a MainWindow.");

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>
    /// Asks for a size (prefilled with the default) and replaces the document with a blank one of
    /// that size. Cancelling, or a size over the memory budget, leaves the document as it was.
    /// </summary>
    public async Task NewAsync()
    {
        if (await Dialogs.AskNewImageSizeAsync(Document.DefaultWidth, Document.DefaultHeight) is not var (width, height))
        {
            return;
        }

        if (!MemoryBudget.Default.Allows((long)width * height * ImageCodec.OpenCostPerPixel))
        {
            await Dialogs.ShowErrorAsync(string.Format(FileStrings.NewImageOverMemoryBudget, width, height));
            return;
        }

        ShowDocument(Document.CreateBlank(width, height), null);
    }

    /// <summary>Opens an image chosen by the user. On failure the current document stays as it was.</summary>
    public async Task OpenAsync()
    {
        if (await Dialogs.PickOpenAsync() is not { } file)
        {
            return;
        }

        Document document;
        try
        {
            await using var stream = await file.OpenReadAsync();
            document = await Task.Run(() => ImageCodec.Open(stream));
        }
        catch (Exception e) when (e is ImageOpenException or IOException or UnauthorizedAccessException)
        {
            var reason = e is ImageOpenException { Failure: var failure } ? failure switch
            {
                OpenFailure.TooLarge => string.Format(FileStrings.TooLargeReason, Document.MaxSide),
                OpenFailure.OverMemoryBudget => FileStrings.OverMemoryBudgetReason,
                _ => FileStrings.UnreadableReason,
            } : FileStrings.ReadErrorReason;
            await Dialogs.ShowErrorAsync(string.Format(FileStrings.CouldNotOpen, file.Name) + Environment.NewLine + reason);
            return;
        }

        ShowDocument(document, file);
    }

    /// <summary>Saves to the current file, or asks where to save if there is none.</summary>
    public async Task SaveAsync()
    {
        if (_file is not null && ImageCodec.FormatOf(_file.Name) is { } format)
        {
            await WriteAsync(_file, format);
        }
        else
        {
            await SaveAsAsync();
        }
    }

    /// <summary>Asks where to save, offering <paramref name="format"/> (default: the current file's format, else PNG).</summary>
    public async Task SaveAsAsync(ImageFormat? format = null)
    {
        var offered = format ?? (_file is null ? null : ImageCodec.FormatOf(_file.Name)) ?? ImageFormat.Png;
        var suggestedName = _file is null ? FileStrings.Untitled : Path.GetFileNameWithoutExtension(_file.Name);
        if (await Dialogs.PickSaveAsync(suggestedName, offered) is not { } file)
        {
            return;
        }

        if (await WriteAsync(file, ImageCodec.FormatOf(file.Name) ?? offered))
        {
            _file = file;
            UpdateTitle();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as MainWindow;
        if (_window is null)
        {
            return;
        }

        _window.AddHandler(KeyDownEvent, OnWindowKeyDown);
        NewItem.InputGesture = new KeyGesture(Key.N, CommandModifiers);
        OpenItem.InputGesture = new KeyGesture(Key.O, CommandModifiers);
        SaveItem.InputGesture = new KeyGesture(Key.S, CommandModifiers);
        SaveAsItem.InputGesture = new KeyGesture(Key.S, CommandModifiers | KeyModifiers.Shift);
        UpdateTitle();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _window?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _window = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ShowDocument(Document document, PickedFile? file)
    {
        Window.ReplaceDocument(document);
        _file = file;
        UpdateTitle();
    }

    private void UpdateTitle() =>
        Window.Title = string.Format(FileStrings.TitleFormat, _file?.Name ?? FileStrings.Untitled, Strings.AppName);

    /// <summary>Exports the composite; encodes to memory first so a failed encode never truncates the file.</summary>
    private async Task<bool> WriteAsync(PickedFile file, ImageFormat format)
    {
        try
        {
            // A pending shape is visible in the composite but not yet in the layer.
            Window.Editor.CommitPending();
            using var encoded = new MemoryStream();
            ImageCodec.Export(Window.Editor.Compositor, encoded, format);
            await using var stream = await file.OpenWriteAsync();
            encoded.Position = 0;
            await encoded.CopyToAsync(stream);
            if (stream.CanSeek)
            {
                stream.SetLength(stream.Position);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowErrorAsync(string.Format(FileStrings.CouldNotSave, file.Name));
            return false;
        }
    }

    // Bubbling, like the history shortcuts, so a focused control can claim the keys first.
    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var command = CommandModifiers;
        Func<Task>? action = (e.Key, e.KeyModifiers) switch
        {
            (Key.N, var m) when m == command => NewAsync,
            (Key.O, var m) when m == command => OpenAsync,
            (Key.S, var m) when m == command => SaveAsync,
            (Key.S, var m) when m == (command | KeyModifiers.Shift) => () => SaveAsAsync(),
            (Key.F12, KeyModifiers.None) => () => SaveAsAsync(),
            _ => null,
        };
        if (action is not null)
        {
            e.Handled = true;
            await action();
        }
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e) => await NewAsync();

    private async void OnOpenClick(object? sender, RoutedEventArgs e) => await OpenAsync();

    private async void OnSaveClick(object? sender, RoutedEventArgs e) => await SaveAsync();

    private async void OnSaveAsPngClick(object? sender, RoutedEventArgs e) => await SaveAsAsync(ImageFormat.Png);

    private async void OnSaveAsJpegClick(object? sender, RoutedEventArgs e) => await SaveAsAsync(ImageFormat.Jpeg);

    private async void OnSaveAsBmpClick(object? sender, RoutedEventArgs e) => await SaveAsAsync(ImageFormat.Bmp);
}
