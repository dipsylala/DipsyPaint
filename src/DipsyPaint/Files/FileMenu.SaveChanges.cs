using Avalonia.Controls;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>
/// The "Save changes?" prompt (ticket 36): before New, Open, closing the window (including Cmd+Q and
/// the macOS app menu's Quit, which Avalonia routes through <see cref="Window.Closing"/>) or "Restart
/// to update" discards unsaved changes, asks whether to save them. Also marks unsaved changes in the
/// window title ("*name - DipsyPaint").
/// </summary>
public partial class FileMenu
{
    private ISaveChangesPrompt? _saveChangesPrompt;
    private DocumentEditor? _editor;

    // Set while the window closes after the prompt, so closing doesn't ask again.
    private bool _closeConfirmed;

    // Set from the prompt until its choice has been carried out, so a second request (e.g. Cmd+Q
    // while it is showing) doesn't ask again.
    private bool _asking;

    /// <summary>The "Save changes?" prompt to use; tests replace it to bypass the dialog.</summary>
    public ISaveChangesPrompt SaveChangesPrompt
    {
        get => _saveChangesPrompt ??= new SaveChangesDialogPrompt(this);
        set => _saveChangesPrompt = value;
    }

    // "Untitled" or the current file's name, as in the title and the prompt.
    private string DocumentName => _file?.Name ?? FileStrings.Untitled;

    // Unsaved changes, or pending content (e.g. a shape not yet committed, a moved selection), which
    // isn't a history step yet but would be lost. A selection that hasn't been moved changes nothing.
    private bool HasChangesToLose =>
        Window.Editor is var editor
        && (editor.HasUnsavedChanges || (editor.Adornment is not null && editor.Selection is not { FloatingPixels: null }));

    /// <summary>
    /// Asks whether to save unsaved changes before they are discarded. Returns true to go on: there
    /// were none, or the user chose Don't save, or Save and the save succeeded. Returns false, leaving
    /// everything as it was, for Cancel or a failed or cancelled save, or while the prompt is already showing.
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!HasChangesToLose)
        {
            return true;
        }

        if (_asking)
        {
            return false;
        }

        _asking = true;
        try
        {
            return await SaveChangesPrompt.AskAsync(DocumentName) switch
            {
                SaveChangesChoice.Save => await SaveAsync(),
                SaveChangesChoice.DontSave => true,
                _ => false,
            };
        }
        finally
        {
            _asking = false;
        }
    }

    /// <summary>
    /// Closes the window after <see cref="ConfirmDiscardChangesAsync"/>, without asking again.
    /// Returns whether the window closed.
    /// </summary>
    public async Task<bool> CloseWindowAsync()
    {
        var window = Window;
        if (!await ConfirmDiscardChangesAsync())
        {
            return false;
        }

        var closed = false;
        void OnClosed(object? sender, EventArgs e) => closed = true;
        window.Closed += OnClosed;
        _closeConfirmed = true;
        try
        {
            window.Close(); // raises Closing, and Closed if nothing cancels it, before returning
        }
        finally
        {
            _closeConfirmed = false;
            window.Closed -= OnClosed;
        }

        return closed;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.UnsavedChangesChanged -= OnUnsavedChangesChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.UnsavedChangesChanged += OnUnsavedChangesChanged;
    }

    private void OnUnsavedChangesChanged(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            UpdateTitle();
        }
    }

    // Closing can't wait for the prompt, so it is cancelled and the window closed again afterwards.
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || !HasChangesToLose)
        {
            return;
        }

        e.Cancel = true;
        await CloseWindowAsync();
    }
}
