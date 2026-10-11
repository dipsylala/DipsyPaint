namespace DipsyPaint.Engine;

/// <summary>
/// The undo/redo list of one document. Holds at most <see cref="CapacityBytes"/> of step memory
/// and drops the oldest steps to stay within it (ADR-0018). Also tracks the position the document
/// was last saved at, so it knows whether there are unsaved changes, whatever the steps are.
/// </summary>
public sealed class History
{
    private readonly List<HistoryStep> _steps = [];

    // Steps before this index are undoable; steps from it onwards are redoable.
    private int _current;

    // The value of _current when the document was last saved, opened or created; -1 once that state
    // can no longer be reached by undo or redo (its steps were discarded or dropped by the cap).
    private int _saved;

    public History() : this(DefaultCapacityBytes)
    {
    }

    public History(long capacityBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacityBytes);
        CapacityBytes = capacityBytes;
    }

    /// <summary>min(1 GB, 25% of the memory available to the process), per ADR-0018.</summary>
    public static long DefaultCapacityBytes { get; } =
        Math.Min(1L << 30, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 4);

    public long CapacityBytes { get; }

    /// <summary>Memory held by all undoable and redoable steps, in bytes.</summary>
    public long SizeBytes { get; private set; }

    public bool CanUndo => _current > 0;
    public bool CanRedo => _current < _steps.Count;

    /// <summary>
    /// True when history steps have been made, undone or redone since the document was last saved,
    /// opened or created (<see cref="MarkSaved"/>): any position other than the saved one.
    /// </summary>
    public bool HasUnsavedChanges => _current != _saved;

    /// <summary>Raised whenever <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when <see cref="HasUnsavedChanges"/> changes, after <see cref="Changed"/>.</summary>
    public event EventHandler? UnsavedChangesChanged;

    /// <summary>Records that the document as it is now has been saved.</summary>
    public void MarkSaved()
    {
        var wasUnsaved = HasUnsavedChanges;
        _saved = _current;
        RaiseUnsavedChangesChanged(wasUnsaved);
    }

    /// <summary>
    /// Records that the document as it is now is not saved anywhere, e.g. one restored from a
    /// recovery snapshot (ADR-0013). Unsaved changes stay until the next <see cref="MarkSaved"/>.
    /// </summary>
    public void MarkUnsaved()
    {
        var wasUnsaved = HasUnsavedChanges;
        _saved = -1;
        RaiseUnsavedChangesChanged(wasUnsaved);
    }

    /// <summary>
    /// Adds a step that has just been applied. Discards the redoable steps, then drops the
    /// oldest steps until the history fits its cap (possibly <paramref name="step"/> itself).
    /// </summary>
    public void Record(HistoryStep step)
    {
        var wasUnsaved = HasUnsavedChanges;
        if (_saved > _current)
        {
            _saved = -1; // the saved state was in the redo steps being discarded
        }

        RemoveSteps(_current, _steps.Count - _current);
        _steps.Add(step);
        _current++;
        SizeBytes += step.SizeBytes;

        while (_steps.Count > 0 && SizeBytes > CapacityBytes)
        {
            RemoveSteps(0, 1);
            _current--;
            _saved = _saved > 0 ? _saved - 1 : -1; // the state before the dropped step is gone
        }

        Changed?.Invoke(this, EventArgs.Empty);
        RaiseUnsavedChangesChanged(wasUnsaved);
    }

    /// <summary>Undoes the latest undoable step; returns the changed rectangle, or empty if there was none.</summary>
    public IntRect Undo()
    {
        if (!CanUndo)
        {
            return IntRect.Empty;
        }

        var wasUnsaved = HasUnsavedChanges;
        var dirty = _steps[--_current].Undo();
        Changed?.Invoke(this, EventArgs.Empty);
        RaiseUnsavedChangesChanged(wasUnsaved);
        return dirty;
    }

    /// <summary>Redoes the earliest redoable step; returns the changed rectangle, or empty if there was none.</summary>
    public IntRect Redo()
    {
        if (!CanRedo)
        {
            return IntRect.Empty;
        }

        var wasUnsaved = HasUnsavedChanges;
        var dirty = _steps[_current++].Redo();
        Changed?.Invoke(this, EventArgs.Empty);
        RaiseUnsavedChangesChanged(wasUnsaved);
        return dirty;
    }

    /// <summary>Discards every step, e.g. when the document is replaced. Unsaved changes stay unsaved.</summary>
    public void Clear()
    {
        var wasUnsaved = HasUnsavedChanges;
        RemoveSteps(0, _steps.Count);
        _current = 0;
        _saved = wasUnsaved ? -1 : 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseUnsavedChangesChanged(bool wasUnsaved)
    {
        if (HasUnsavedChanges != wasUnsaved)
        {
            UnsavedChangesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RemoveSteps(int index, int count)
    {
        for (var i = index; i < index + count; i++)
        {
            SizeBytes -= _steps[i].SizeBytes;
        }

        _steps.RemoveRange(index, count);
    }
}
