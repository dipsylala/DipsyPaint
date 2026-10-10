namespace DipsyPaint.Engine;

/// <summary>
/// The undo/redo list of one document. Holds at most <see cref="CapacityBytes"/> of step memory
/// and drops the oldest steps to stay within it (ADR-0018).
/// </summary>
public sealed class History
{
    private readonly List<HistoryStep> _steps = [];

    // Steps before this index are undoable; steps from it onwards are redoable.
    private int _current;

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

    /// <summary>Raised whenever <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Adds a step that has just been applied. Discards the redoable steps, then drops the
    /// oldest steps until the history fits its cap (possibly <paramref name="step"/> itself).
    /// </summary>
    public void Record(HistoryStep step)
    {
        RemoveSteps(_current, _steps.Count - _current);
        _steps.Add(step);
        _current++;
        SizeBytes += step.SizeBytes;

        while (_steps.Count > 0 && SizeBytes > CapacityBytes)
        {
            RemoveSteps(0, 1);
            _current--;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the latest undoable step; returns the changed rectangle, or empty if there was none.</summary>
    public IntRect Undo()
    {
        if (!CanUndo)
        {
            return IntRect.Empty;
        }

        var dirty = _steps[--_current].Undo();
        Changed?.Invoke(this, EventArgs.Empty);
        return dirty;
    }

    /// <summary>Redoes the earliest redoable step; returns the changed rectangle, or empty if there was none.</summary>
    public IntRect Redo()
    {
        if (!CanRedo)
        {
            return IntRect.Empty;
        }

        var dirty = _steps[_current++].Redo();
        Changed?.Invoke(this, EventArgs.Empty);
        return dirty;
    }

    /// <summary>Discards every step, e.g. when the document is replaced.</summary>
    public void Clear()
    {
        RemoveSteps(0, _steps.Count);
        _current = 0;
        Changed?.Invoke(this, EventArgs.Empty);
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
