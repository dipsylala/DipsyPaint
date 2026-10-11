namespace DipsyPaint.Engine.Tools;

public enum PointerAction
{
    Down,
    Move,
    Up,
}

public enum PointerButton
{
    Left,
    Right,
}

/// <summary>
/// One pointer event as data, so tools can be scripted without a UI (ADR-0016).
/// <paramref name="X"/> and <paramref name="Y"/> are in document pixels: pixel (i, j) covers
/// [i, i+1) × [j, j+1). Move and Up carry the button that started the stroke.
/// <paramref name="Pressure"/> is 0–1; 1 when the device reports none (a mouse). Brushes scale their
/// tip by it, and ignore it on Up, where a pen lifting off reports none (<see cref="PenPressure"/>).
/// <paramref name="Shift"/> is whether Shift is held (it constrains shapes).
/// <paramref name="Time"/> is when the event happened, from an arbitrary fixed origin; null when unknown
/// (scripts), and tools that need it then assume events <see cref="NominalInterval"/> apart.
/// </summary>
public readonly record struct PointerInput(
    PointerAction Action,
    double X,
    double Y,
    PointerButton Button = PointerButton.Left,
    float Pressure = 1f,
    bool Shift = false,
    TimeSpan? Time = null)
{
    /// <summary>The time assumed between events without a <see cref="Time"/>: one frame at 60 Hz.</summary>
    public static TimeSpan NominalInterval { get; } = TimeSpan.FromMilliseconds(16);
}
