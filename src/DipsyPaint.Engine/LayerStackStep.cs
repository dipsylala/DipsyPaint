namespace DipsyPaint.Engine;

/// <summary>
/// A history step for a change to the layer stack (layers added, removed or moved, properties or the
/// active layer changed), optionally with pixels written to a layer as part of it (merge down). It
/// holds the other version of the stack; undo and redo swap it with the document's. Removed layers
/// stay alive in the step, so restoring them restores their pixels.
/// </summary>
internal sealed class LayerStackStep : HistoryStep
{
    private readonly Document _document;
    private readonly HistoryStep? _pixels;
    private LayerStack _stored;

    /// <param name="document">The document after the change.</param>
    /// <param name="before">Its stack before the change.</param>
    /// <param name="pixels">The step for pixels the change wrote, if any.</param>
    public LayerStackStep(Document document, LayerStack before, HistoryStep? pixels)
    {
        _document = document;
        _stored = before;
        _pixels = pixels;
        // Constant for the history's accounting: the layers only one of the two versions has, which
        // only this step keeps alive while that version is not shown.
        var after = document.Layers;
        var onlyInOne = before.Layers.Except(after).Concat(after.Except(before.Layers));
        SizeBytes = onlyInOne.Sum(l => l.SizeBytes) + (pixels?.SizeBytes ?? 0);
    }

    public override long SizeBytes { get; }

    public override IntRect Undo()
    {
        Swap();
        _pixels?.Undo();
        return Canvas;
    }

    public override IntRect Redo()
    {
        _pixels?.Redo();
        Swap();
        return Canvas;
    }

    private IntRect Canvas => new(0, 0, _document.Width, _document.Height);

    private void Swap()
    {
        var current = _document.SaveStack();
        _document.RestoreStack(_stored);
        _stored = current;
    }
}
