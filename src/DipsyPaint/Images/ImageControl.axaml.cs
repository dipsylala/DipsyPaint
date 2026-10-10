using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Images;

/// <summary>
/// Reference Paint's Image group: Crop (to the selection), Resize, and the Rotate menu (rotate right
/// or left 90°, rotate 180°, flip vertical or horizontal), for the <see cref="DocumentEditor"/> in the
/// DataContext. With a selection, resize, rotate and flip apply to it, else to the whole picture.
/// Also handles Paint's shortcuts for the whole window: Ctrl+W (Resize) and Ctrl+Shift+X (Crop),
/// Cmd on macOS.
/// </summary>
public partial class ImageControl : UserControl
{
    private DocumentEditor? _editor;
    private TopLevel? _topLevel;

    public ImageControl()
    {
        InitializeComponent();
        Dialogs = new WindowImageDialogs(this);
    }

    /// <summary>The dialogs to use; tests replace these to bypass real windows.</summary>
    public IImageDialogs Dialogs { get; set; }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Crops the picture to the selection; does nothing without one.</summary>
    public void Crop() => _editor?.CropToSelection();

    /// <summary>Rotates or flips the selection, or the whole picture without one.</summary>
    public void Transform(ImageTransform transform) => _editor?.Transform(transform);

    /// <summary>
    /// Asks for a new size for the selection, or the whole picture without one, and resizes it;
    /// a size the editor refuses is reported in a message.
    /// </summary>
    public async Task ResizeAsync()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var (width, height) = editor.Selection is { Bounds: var bounds }
            ? (bounds.Width, bounds.Height)
            : (editor.Document.Width, editor.Document.Height);
        if (await Dialogs.AskResizeAsync(width, height) is not { } size)
        {
            return;
        }

        var message = editor.Resize(size.Width, size.Height) switch
        {
            ResizeResult.TooLarge => string.Format(ImageStrings.TooLarge, size.Width, size.Height, Document.MaxSide),
            ResizeResult.OverMemoryBudget => string.Format(ImageStrings.OverMemoryBudget, size.Width, size.Height),
            _ => null,
        };
        if (message is not null)
        {
            await Dialogs.ShowErrorAsync(message);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor = DataContext as DocumentEditor;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Bubbling, like the other window shortcuts, so a focused control can claim the keys first.
    private async void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        var command = CommandModifiers;
        if (e.Key == Key.W && e.KeyModifiers == command)
        {
            e.Handled = true;
            await ResizeAsync();
        }
        else if (e.Key == Key.X && e.KeyModifiers == (command | KeyModifiers.Shift))
        {
            e.Handled = true;
            Crop();
        }
    }

    private void OnCropClick(object? sender, RoutedEventArgs e) => Crop();

    private async void OnResizeClick(object? sender, RoutedEventArgs e) => await ResizeAsync();

    private void OnRotateRightClick(object? sender, RoutedEventArgs e) => Transform(ImageTransform.RotateRight);

    private void OnRotateLeftClick(object? sender, RoutedEventArgs e) => Transform(ImageTransform.RotateLeft);

    private void OnRotate180Click(object? sender, RoutedEventArgs e) => Transform(ImageTransform.Rotate180);

    private void OnFlipVerticalClick(object? sender, RoutedEventArgs e) => Transform(ImageTransform.FlipVertical);

    private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e) => Transform(ImageTransform.FlipHorizontal);
}
