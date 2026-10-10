namespace DipsyPaint.Engine.Codecs;

/// <summary>Why a file could not be opened.</summary>
public enum OpenFailure
{
    /// <summary>The file is corrupt, truncated or not a supported format.</summary>
    Unreadable,

    /// <summary>The image is wider or taller than <see cref="Document.MaxSide"/>.</summary>
    TooLarge,

    /// <summary>Opening the image would exceed the <see cref="MemoryBudget"/>.</summary>
    OverMemoryBudget,
}

/// <summary>Thrown when a file cannot be opened; the caller's current document is untouched.</summary>
public sealed class ImageOpenException(OpenFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public OpenFailure Failure { get; } = failure;
}
