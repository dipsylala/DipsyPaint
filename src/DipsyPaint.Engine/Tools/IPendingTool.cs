namespace DipsyPaint.Engine.Tools;

/// <summary>
/// A tool whose result stays pending between gestures: shown in <see cref="ToolContext.Overlay"/>
/// (not on the layer, not in history) and adjustable until committed, as reference Paint's shapes
/// and selections are. The tool commits by itself when a gesture starts outside its content; the
/// <see cref="DocumentEditor"/> commits it on tool switch, undo and <see cref="DocumentEditor.CommitPending"/>
/// (Enter), recording one history step, and cancels it on <see cref="DocumentEditor.CancelPending"/> (Esc).
/// </summary>
public interface IPendingTool : ITool
{
    /// <summary>The frame and handles to show around the pending content; null when there is none to adjust.</summary>
    Adornment? Adornment { get; }

    /// <summary>
    /// Writes the pending content to <see cref="ToolContext.Target"/> and clears it from the overlay.
    /// Returns the changed rectangle; empty when nothing was pending.
    /// </summary>
    IntRect Commit(ToolContext context);

    /// <summary>Discards the pending content without touching the layer. Returns the changed rectangle.</summary>
    IntRect Cancel(ToolContext context);

    /// <summary>
    /// Re-renders the pending content after the colours or the tool's options changed. Must change
    /// only the overlay. Returns the changed rectangle.
    /// </summary>
    IntRect Refresh(ToolContext context);
}
