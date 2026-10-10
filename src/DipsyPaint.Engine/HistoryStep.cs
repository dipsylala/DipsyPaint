namespace DipsyPaint.Engine;

/// <summary>
/// One undoable action. A step is recorded after its action has been applied, so the first call
/// is <see cref="Undo"/>; after that, <see cref="Undo"/> and <see cref="Redo"/> alternate.
/// </summary>
public abstract class HistoryStep
{
    /// <summary>Memory the step holds, in bytes; counted against the history's cap.</summary>
    public abstract long SizeBytes { get; }

    /// <summary>Reverts the action and returns the document rectangle whose pixels changed.</summary>
    public abstract IntRect Undo();

    /// <summary>Reapplies the action and returns the document rectangle whose pixels changed.</summary>
    public abstract IntRect Redo();
}
