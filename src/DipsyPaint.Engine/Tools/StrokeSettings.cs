namespace DipsyPaint.Engine.Tools;

/// <summary>
/// User settings of a stroke tool, shared by the tool and its options panel so they survive
/// reselecting the tool. A tool reads them when a stroke starts.
/// </summary>
public sealed class StrokeSettings
{
    public const int MinSize = 1;
    public const int MaxSize = 100;

    public StrokeSettings(int size) => Size = size;

    /// <summary>Tip size in pixels (diameter or side), clamped to <see cref="MinSize"/>–<see cref="MaxSize"/>.</summary>
    public int Size
    {
        get;
        set => field = Math.Clamp(value, MinSize, MaxSize);
    }
}
