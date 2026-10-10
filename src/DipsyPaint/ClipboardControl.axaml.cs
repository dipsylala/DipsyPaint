using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Resources;

namespace DipsyPaint;

/// <summary>
/// Reference Paint's Clipboard group: Paste, Cut and Copy for the <see cref="DocumentEditor"/> in the
/// DataContext, through the OS clipboard. Also handles their shortcuts for the whole window: Ctrl+V,
/// Ctrl+X and Ctrl+C (Cmd on macOS). Cut and Copy act on the selection and are disabled without one.
/// Pasted content arrives as a selection, already lifted, at the top-left of the visible part of the
/// canvas, which grows to fit it if needed, as in reference Paint.
/// </summary>
public partial class ClipboardControl : UserControl
{
    private DocumentEditor? _editor;
    private TopLevel? _topLevel;

    public ClipboardControl()
    {
        InitializeComponent();
        ImageClipboard = new AvaloniaImageClipboard(this);
    }

    /// <summary>The clipboard to use; tests replace it.</summary>
    public IImageClipboard ImageClipboard { get; set; }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Puts the selected pixels on the clipboard; does nothing without a selection.</summary>
    public async Task CopyAsync()
    {
        if (_editor?.CopySelection() is { } image)
        {
            await SetAsync(image);
        }
    }

    /// <summary>Puts the selected pixels on the clipboard, then deletes them if that worked.</summary>
    public async Task CutAsync()
    {
        if (_editor is { } editor && editor.CopySelection() is { } image && await SetAsync(image))
        {
            editor.DeleteSelection();
        }
    }

    /// <summary>Pastes the image on the clipboard, if any; a damaged one shows a message instead.</summary>
    public async Task PasteAsync()
    {
        PixelImage? image;
        try
        {
            image = await ImageClipboard.GetImageAsync();
        }
        catch (ImageOpenException e)
        {
            var reason = e.Failure switch
            {
                OpenFailure.TooLarge => string.Format(ClipboardStrings.TooLargeReason, Document.MaxSide),
                OpenFailure.OverMemoryBudget => ClipboardStrings.OverMemoryBudgetReason,
                _ => ClipboardStrings.UnreadableReason,
            };
            await ImageClipboard.ShowErrorAsync(ClipboardStrings.CouldNotPaste + Environment.NewLine + reason);
            return;
        }

        if (image is not null && _editor is { } editor)
        {
            var (x, y) = PastePosition(editor);
            editor.Paste(image, x, y);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.SelectionChanged -= OnSelectionChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.SelectionChanged += OnSelectionChanged;
        UpdateButtons();
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

    private async Task<bool> SetAsync(PixelImage image)
    {
        try
        {
            await ImageClipboard.SetImageAsync(image);
            return true;
        }
        catch (IOException)
        {
            await ImageClipboard.ShowErrorAsync(ClipboardStrings.CouldNotCopy);
            return false;
        }
    }

    // The canvas pixel at the top-left of the visible area: (0, 0) unless the canvas is scrolled.
    private (int X, int Y) PastePosition(DocumentEditor editor)
    {
        var scroll = _topLevel?.FindControl<ScrollViewer>("CanvasScroll");
        var canvas = _topLevel?.FindControl<DocumentCanvas>("DocumentView");
        if (scroll is null || canvas is null || scroll.TranslatePoint(default, canvas) is not { } corner)
        {
            return (0, 0);
        }

        return (Math.Clamp((int)Math.Ceiling(corner.X), 0, editor.Document.Width - 1),
            Math.Clamp((int)Math.Ceiling(corner.Y), 0, editor.Document.Height - 1));
    }

    // Bubbling, so a focused control with its own clipboard (e.g. a text box) handles the keys first.
    private async void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != CommandModifiers)
        {
            return;
        }

        Func<Task>? action = e.Key switch
        {
            Key.V => PasteAsync,
            Key.X => CutAsync,
            Key.C => CopyAsync,
            _ => null,
        };
        if (action is not null)
        {
            e.Handled = true;
            await action();
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        CutButton.IsEnabled = CopyButton.IsEnabled = _editor?.Selection is not null;
    }

    private async void OnPasteClick(object? sender, RoutedEventArgs e) => await PasteAsync();

    private async void OnCutClick(object? sender, RoutedEventArgs e) => await CutAsync();

    private async void OnCopyClick(object? sender, RoutedEventArgs e) => await CopyAsync();
}
