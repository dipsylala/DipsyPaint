namespace DipsyPaint.Engine;

/// <summary>The outcome of <see cref="DocumentEditor.Resize"/>.</summary>
public enum ResizeResult
{
    Resized,

    /// <summary>Refused: a side would exceed <see cref="Document.MaxSide"/>.</summary>
    TooLarge,

    /// <summary>Refused: the new pixels would exceed the <see cref="MemoryBudget"/>.</summary>
    OverMemoryBudget,
}
