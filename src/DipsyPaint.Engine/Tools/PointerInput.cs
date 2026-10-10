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
/// <paramref name="Pressure"/> is 0–1; 1 when the device reports none.
/// <paramref name="Shift"/> is whether Shift is held (it constrains shapes).
/// </summary>
public readonly record struct PointerInput(
    PointerAction Action,
    double X,
    double Y,
    PointerButton Button = PointerButton.Left,
    float Pressure = 1f,
    bool Shift = false);
