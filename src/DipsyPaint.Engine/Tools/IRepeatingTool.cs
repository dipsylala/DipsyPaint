namespace DipsyPaint.Engine.Tools;

/// <summary>
/// A tool that keeps working while the pointer is held still, such as the airbrush. While a stroke
/// is held, the shell repeats its last pointer input as a <see cref="PointerAction.Move"/>, with the
/// current <see cref="PointerInput.Time"/>, every <see cref="RepeatInterval"/>.
/// </summary>
public interface IRepeatingTool
{
    TimeSpan RepeatInterval { get; }
}
