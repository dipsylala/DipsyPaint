using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Engine;

namespace DipsyPaint;

/// <summary>
/// Undo and Redo buttons for the <see cref="DocumentEditor"/> in the DataContext. Also handles
/// reference Paint's shortcuts for the whole window: Ctrl+Z and Ctrl+Y (Cmd on macOS).
/// </summary>
public partial class HistoryControl : UserControl
{
    private DocumentEditor? _editor;
    private TopLevel? _topLevel;

    public HistoryControl() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.History.Changed -= OnHistoryChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.History.Changed += OnHistoryChanged;
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

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    // Bubbling, so a focused control that has its own undo (e.g. a text box) handles the key first.
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is null || e.KeyModifiers != CommandModifiers)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z:
                _editor.Undo();
                e.Handled = true;
                break;
            case Key.Y:
                _editor.Redo();
                e.Handled = true;
                break;
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        UndoButton.IsEnabled = _editor?.History.CanUndo == true;
        RedoButton.IsEnabled = _editor?.History.CanRedo == true;
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _editor?.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _editor?.Redo();
}
