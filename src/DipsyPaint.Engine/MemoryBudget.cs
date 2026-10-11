namespace DipsyPaint.Engine;

/// <summary>
/// The most memory one large operation (open, new layer, duplicate layer, canvas resize) may need
/// before it is refused with a message instead of risking a crash (ADR-0013).
/// </summary>
public sealed record MemoryBudget(long Bytes)
{
    /// <summary>The budget on a machine with plenty of memory: 2 GiB.</summary>
    public const long MaxDefaultBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// min(2 GiB, 50% of the memory available to the process). 2 GiB holds the scale target (4K × 20
    /// layers plus the compositor's buffers, about 0.85 GB) more than twice over. Half the memory keeps
    /// the budget plus the undo cap (at most 25%, ADR-0018) within 75% of it on small machines, where
    /// a fixed 2 GiB would let one operation exhaust it. Callers pass their own budget to override it.
    /// </summary>
    public static MemoryBudget Default { get; } = For(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

    /// <summary>The default budget for a process with <paramref name="availableBytes"/> of memory.</summary>
    public static MemoryBudget For(long availableBytes) => new(Math.Min(MaxDefaultBytes, availableBytes / 2));

    public bool Allows(long bytes) => bytes <= Bytes;
}
