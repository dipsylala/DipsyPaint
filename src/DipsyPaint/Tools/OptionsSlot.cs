using Avalonia;
using Avalonia.Controls;

namespace DipsyPaint.Tools;

/// <summary>
/// Holds the active tool's options panel and takes up at least <see cref="Reserve"/>, never more
/// than the space available, so the command bar keeps its size whichever tool is active.
/// </summary>
public sealed class OptionsSlot : Decorator
{
    private Size _reserve;

    /// <summary>The smallest size the slot takes, e.g. that of the largest options panel.</summary>
    public Size Reserve
    {
        get => _reserve;
        set
        {
            _reserve = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(
            Math.Min(Math.Max(size.Width, _reserve.Width), availableSize.Width),
            Math.Min(Math.Max(size.Height, _reserve.Height), availableSize.Height));
    }
}
