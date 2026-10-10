namespace DipsyPaint.Engine;

/// <summary>
/// The most memory one large operation (opening an image, and later new layer or canvas resize)
/// may need before it is refused with a message instead of risking a crash (ADR-0013).
/// </summary>
public sealed record MemoryBudget(long Bytes)
{
    /// <summary>
    /// Placeholder: 2 GiB. The real value is not quantified yet (spec, unresolved question 3);
    /// tune it during M1/M2. Callers pass their own <see cref="MemoryBudget"/> to override it.
    /// </summary>
    public static MemoryBudget Default { get; } = new(2L * 1024 * 1024 * 1024);

    public bool Allows(long bytes) => bytes <= Bytes;
}
