namespace DipsyPaint.Engine.Tools;

/// <summary>
/// A tool whose result stays pending between gestures: shown in <see cref="ToolContext.Overlay"/>
/// and adjustable until committed, as reference Paint's shapes and selections are. The
/// <see cref="DocumentEditor"/> commits it when a press lands outside <see cref="Adornment"/>
/// (before passing that press on), on tool switch, on undo and on <see cref="DocumentEditor.CommitPending"/>
/// (Enter); it cancels it on <see cref="DocumentEditor.CancelPending"/> (Esc). Layer writes made
/// through <see cref="ToolContext.Target"/> while content is pending (e.g. the hole a lifted
/// selection leaves) and by <see cref="Commit"/> form one history step; cancelling reverts them.
/// </summary>
public interface IPendingTool : ITool
{
    /// <summary>
    /// The frame and handles around the pending content; non-null exactly while content is pending
    /// between gestures (it may be null during the gesture that creates it).
    /// </summary>
    Adornment? Adornment { get; }

    /// <summary>
    /// Writes the pending content to <see cref="ToolContext.Target"/> and clears it from the overlay.
    /// Returns the changed rectangle; empty when nothing was pending.
    /// </summary>
    IntRect Commit(ToolContext context);

    /// <summary>Discards the pending content from the overlay. Returns the changed rectangle.</summary>
    IntRect Cancel(ToolContext context);

    /// <summary>
    /// Re-renders the pending content after the colours or the tool's options changed. Must change
    /// only the overlay. Returns the changed rectangle.
    /// </summary>
    IntRect Refresh(ToolContext context);
}
