namespace DipsyPaint.Recovery;

/// <summary>The answer to the recovery prompt.</summary>
public enum RecoveryChoice
{
    /// <summary>Open the snapshot as an untitled document with unsaved changes.</summary>
    Restore,

    /// <summary>Delete the snapshot.</summary>
    Discard,

    /// <summary>Keep the snapshot and ask again next launch (until it is stale).</summary>
    Later,
}

/// <summary>
/// Asks what to do with a snapshot left by a crash. <see cref="RecoveryDialog"/> is the real prompt;
/// tests substitute their own.
/// </summary>
public interface IRecoveryPrompt
{
    Task<RecoveryChoice> AskAsync(RecoverySnapshot snapshot);
}
