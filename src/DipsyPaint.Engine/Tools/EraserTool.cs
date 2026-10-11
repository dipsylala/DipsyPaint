namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's eraser: a hard-edged square of <see cref="StrokeSettings.Size"/> pixels,
/// centred on the pixel under the pointer, that erases the active layer to transparency (Paint
/// shows the transparency as a checkerboard on a single-layer document). Below 100 %
/// <see cref="StrokeSettings.Opacity"/> it leaves pixels partially transparent. Both buttons erase.
/// </summary>
public sealed class EraserTool(StrokeSettings settings) : ITool
{
    private Stroke? _stroke;
    private int _size;
    private int _lastLeft;
    private int _lastTop;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        if (input.Action == PointerAction.Down)
        {
            _stroke = Stroke.Erase(context.Target, settings.OpacityFraction);
            _size = settings.Size;
        }
        else if (_stroke is null)
        {
            return IntRect.Empty;
        }

        var offset = (_size - 1) / 2;
        var left = (int)Math.Floor(input.X) - offset;
        var top = (int)Math.Floor(input.Y) - offset;
        if (input.Action == PointerAction.Down)
        {
            Fill(_stroke, left, top, _size, _size);
        }
        else
        {
            Sweep(_stroke, _lastLeft, _lastTop, left, top, _size);
        }

        _lastLeft = left;
        _lastTop = top;
        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            _stroke = null;
        }

        return dirty;
    }

    /// <summary>
    /// Moves the square one pixel step at a time from one top-left corner to another and covers
    /// the row and/or column each step uncovers, so fast moves leave no gaps.
    /// </summary>
    private static void Sweep(Stroke stroke, int left0, int top0, int left1, int top1, int size)
    {
        int previousLeft = left0, previousTop = top0;
        foreach (var (left, top) in Stroke.LinePoints(left0, top0, left1, top1))
        {
            if (left != previousLeft)
            {
                Fill(stroke, left > previousLeft ? left + size - 1 : left, top, 1, size);
            }

            if (top != previousTop)
            {
                Fill(stroke, left, top > previousTop ? top + size - 1 : top, size, 1);
            }

            previousLeft = left;
            previousTop = top;
        }
    }

    private static void Fill(Stroke stroke, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                stroke.Plot(x, y);
            }
        }
    }
}
