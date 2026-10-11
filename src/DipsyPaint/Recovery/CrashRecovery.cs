using Avalonia.Threading;
using DipsyPaint.Diagnostics;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Resources;

namespace DipsyPaint.Recovery;

/// <summary>
/// Crash recovery for a <see cref="MainWindow"/> (ADR-0013, an addition per ADR-0011). While the
/// document has unsaved changes, a snapshot is taken every <see cref="Interval"/>: the pixels are
/// copied on the UI thread, then encoded and written to the <see cref="RecoveryStore"/> on a
/// background thread. The snapshot is deleted when the document is saved, its changes are undone,
/// it is replaced (New or Open, after the "Save changes?" prompt) or the window closes. A crash on the
/// UI thread (out-of-memory included) writes an emergency snapshot beside the crash log.
/// <see cref="OfferRestoreAsync"/> offers the snapshots of sessions that did not exit cleanly.
/// </summary>
public sealed class CrashRecovery
{
    private readonly MainWindow _window;
    private readonly TimeProvider _time;
    private readonly CrashHandler? _crashes;
    private DocumentEditor? _editor;
    private ITimer? _timer;

    /// <summary>Starts recovery for <paramref name="window"/>; throws if the store's folder can't be written.</summary>
    /// <param name="window">The window whose documents are protected.</param>
    /// <param name="store">The recovery folder; its session begins here.</param>
    /// <param name="interval">The time between snapshots while there are unsaved changes.</param>
    /// <param name="time">The clock and timers; tests pass a fake.</param>
    /// <param name="crashes">The crash handler to take emergency snapshots for, if any.</param>
    public CrashRecovery(MainWindow window, RecoveryStore store, TimeSpan interval, TimeProvider? time = null, CrashHandler? crashes = null)
    {
        _window = window;
        Store = store;
        Interval = interval;
        _time = time ?? TimeProvider.System;
        Prompt = new RecoveryDialogPrompt(window);
        store.BeginSession();
        window.DataContextChanged += (_, _) => OnDocumentReplaced();
        window.Closed += (_, _) => End();
        _crashes = crashes;
        _crashes?.Crashed += OnCrashed;
        OnDocumentReplaced();
    }

    public RecoveryStore Store { get; }

    public TimeSpan Interval { get; }

    /// <summary>The recovery prompt to use; tests replace it to bypass the dialog.</summary>
    public IRecoveryPrompt Prompt { get; set; }

    /// <summary>The latest background snapshot write; completed when none is in progress.</summary>
    public Task PendingWrite { get; private set; } = Task.CompletedTask;

    /// <summary>Whether snapshots are being taken (there are unsaved changes).</summary>
    public bool IsTakingSnapshots => _timer is not null;

    /// <summary>
    /// Asks, for each snapshot a crashed session left (newest first), whether to restore or discard it.
    /// Restoring asks to save the current document's changes first, then shows the snapshot as an
    /// untitled document with unsaved changes. Snapshots left for later are offered again next launch.
    /// </summary>
    public async Task OfferRestoreAsync()
    {
        foreach (var snapshot in Store.FindOrphans())
        {
            switch (await Prompt.AskAsync(snapshot))
            {
                case RecoveryChoice.Restore:
                    await RestoreAsync(snapshot);
                    break;
                case RecoveryChoice.Discard:
                    Store.Discard(snapshot);
                    break;
                default:
                    Store.Release(snapshot);
                    break;
            }
        }
    }

    /// <summary>
    /// Takes a snapshot now if there are unsaved changes and no write is in progress: copies the
    /// pixels (UI thread), then writes them on a background thread (<see cref="PendingWrite"/>).
    /// The timer calls this every <see cref="Interval"/>.
    /// </summary>
    public void TakeSnapshot()
    {
        if (_editor is not { HasUnsavedChanges: true } editor || !PendingWrite.IsCompleted)
        {
            return;
        }

        var sequence = Store.Reserve();
        OpenRasterSnapshot snapshot;
        try
        {
            snapshot = OpenRasterSnapshot.Capture(editor.Compositor);
        }
        catch (OutOfMemoryException)
        {
            return; // try again next time; the previous snapshot stays
        }

        var info = new RecoveryInfo(_window.FileMenu.DocumentName, _time.GetUtcNow());
        PendingWrite = Task.Run(() =>
        {
            try
            {
                Store.Write(sequence, snapshot.WriteTo, info);
            }
            catch
            {
                // Best effort: a failed snapshot must never take the app down; the previous one stays.
            }
        });
    }

    private async Task RestoreAsync(RecoverySnapshot snapshot)
    {
        var menu = _window.FileMenu;
        if (!await menu.ConfirmDiscardChangesAsync())
        {
            Store.Release(snapshot);
            return;
        }

        Document document;
        try
        {
            await using var stream = RecoveryStore.OpenRead(snapshot);
            document = await Task.Run(() => ImageCodec.Open(stream));
        }
        catch (Exception e) when (e is ImageOpenException or IOException or UnauthorizedAccessException)
        {
            Store.Release(snapshot);
            var reason = e is ImageOpenException { Failure: OpenFailure.OverMemoryBudget }
                ? RecoveryStrings.OverMemoryBudget
                : RecoveryStrings.Damaged;
            await menu.Dialogs.ShowErrorAsync(
                string.Format(RecoveryStrings.CouldNotRestore, snapshot.DocumentName ?? FileStrings.Untitled) + Environment.NewLine + reason);
            return;
        }

        menu.ShowUntitled(document); // deletes this session's snapshot of the document it replaces
        try
        {
            Store.Adopt(snapshot);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The next snapshot replaces it; the crashed session's copy is offered again next launch.
            Store.Release(snapshot);
        }

        _window.Editor.History.MarkUnsaved();
    }

    private void OnDocumentReplaced()
    {
        _editor?.UnsavedChangesChanged -= OnUnsavedChangesChanged;
        _editor = _window.DataContext as DocumentEditor;
        _editor?.UnsavedChangesChanged += OnUnsavedChangesChanged;
        Store.Delete();
        UpdateTimer();
    }

    private void OnUnsavedChangesChanged(object? sender, EventArgs e)
    {
        if (_editor is { HasUnsavedChanges: false })
        {
            Store.Delete();
        }

        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (_editor is { HasUnsavedChanges: true })
        {
            _timer ??= _time.CreateTimer(_ => Dispatcher.UIThread.Post(TakeSnapshot), null, Interval, Interval);
        }
        else
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    // A clean exit: the "Save changes?" prompt has been answered.
    private void End()
    {
        _timer?.Dispose();
        _timer = null;
        _crashes?.Crashed -= OnCrashed;
        Store.EndSession();
    }

    // Runs on the crashing thread before the app exits. Only on the UI thread is the document safe to
    // read; it is written straight from the layers, without the copy a periodic snapshot makes, since
    // memory may have run out. Elsewhere the latest periodic snapshot stands.
    private void OnCrashed(Exception exception)
    {
        if (!Dispatcher.UIThread.CheckAccess() || _editor is not { HasUnsavedChanges: true } editor)
        {
            return;
        }

        try
        {
            var info = new RecoveryInfo(_window.FileMenu.DocumentName, _time.GetUtcNow());
            Store.Write(Store.Reserve(), stream => OpenRasterCodec.Save(editor.Compositor, stream), info);
        }
        catch
        {
            // Best effort, and never in the way of the crash log.
        }
    }
}
