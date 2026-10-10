namespace DipsyPaint.Benchmarks;

/// <summary>The ADR-0018 performance target a benchmark tracks, in milliseconds per operation.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TargetAttribute(double milliseconds) : Attribute
{
    public double Milliseconds { get; } = milliseconds;
}
