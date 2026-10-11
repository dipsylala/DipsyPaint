namespace DipsyPaint.Engine.Tools;

/// <summary>
/// User settings of a stroke tool, shared by the tool and its options panel so they survive
/// reselecting the tool. A tool reads them when a stroke starts.
/// </summary>
public sealed class StrokeSettings
{
    public const int MinSize = 1;
    public const int MaxSize = 100;
    public const int MinOpacity = 1;
    public const int MaxOpacity = 100;

    public StrokeSettings(int size) => Size = size;

    /// <summary>Tip size in pixels (diameter or side), clamped to <see cref="MinSize"/>–<see cref="MaxSize"/>.</summary>
    public int Size
    {
        get;
        set => field = Math.Clamp(value, MinSize, MaxSize);
    }

    /// <summary>
    /// Stroke opacity in percent, clamped to <see cref="MinOpacity"/>–<see cref="MaxOpacity"/>. A painting
    /// stroke covers a pixel at most this much; an erasing stroke removes at most this much of its alpha.
    /// </summary>
    public int Opacity
    {
        get;
        set => field = Math.Clamp(value, MinOpacity, MaxOpacity);
    } = MaxOpacity;

    /// <summary><see cref="Opacity"/> as a fraction (0–1), as <see cref="Stroke"/> takes it.</summary>
    public float OpacityFraction => Opacity / 100f;
}
