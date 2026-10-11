namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// One of reference Paint's artistic brushes: sweeps a <see cref="BrushTip"/> of
/// <see cref="StrokeSettings.Size"/> pixels along the pointer path, as one <see cref="Stroke"/> at
/// <see cref="StrokeSettings.Opacity"/>. The left button paints in the primary colour, the right
/// button in the secondary colour. The tip is centred on the pixel under the pointer. Pen pressure
/// scales the tip's size, tapering between events (<see cref="PenPressure"/>).
/// </summary>
public sealed class ArtisticBrushTool(StrokeSettings settings, BrushTip tip) : ITool
{
    private Stroke? _stroke;
    private double _size;
    private double _lastSize;
    private double _lastX;
    private double _lastY;

    /// <summary>How this brush marks pixels.</summary>
    public BrushTip Tip => tip;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = Math.Floor(input.X) + 0.5;
        var y = Math.Floor(input.Y) + 0.5;

        if (input.Action == PointerAction.Down)
        {
            var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
            _stroke = new Stroke(context.Target, color, settings.OpacityFraction);
            _size = settings.Size;
            _lastSize = PenPressure.ScaleSize(_size, input.Pressure);
            _lastX = x;
            _lastY = y;
        }
        else if (_stroke is null)
        {
            return IntRect.Empty;
        }

        var size = input.Action == PointerAction.Up ? _lastSize : PenPressure.ScaleSize(_size, input.Pressure);
        tip.Segment(_stroke, _lastX, _lastY, x, y, _lastSize, size);
        _lastX = x;
        _lastY = y;
        _lastSize = size;
        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            _stroke = null;
        }

        return dirty;
    }
}
