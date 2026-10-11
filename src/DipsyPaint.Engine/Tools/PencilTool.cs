namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Hard-edged, aliased 1 px freehand line, as reference Paint's pencil: the left button
/// draws in the primary colour, the right button in the secondary colour, at
/// <see cref="StrokeSettings.Opacity"/>. The pencil ignores <see cref="StrokeSettings.Size"/>.
/// </summary>
public sealed class PencilTool(StrokeSettings settings) : ITool
{
    /// <summary>Creates a fully opaque pencil.</summary>
    public PencilTool() : this(new StrokeSettings(StrokeSettings.MinSize))
    {
    }

    private Stroke? _stroke;
    private int _lastX;
    private int _lastY;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = (int)Math.Floor(input.X);
        var y = (int)Math.Floor(input.Y);

        if (input.Action == PointerAction.Down)
        {
            var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
            _stroke = new Stroke(context.Target, color, settings.OpacityFraction);
            _stroke.Plot(x, y);
        }
        else if (_stroke is not null)
        {
            _stroke.Line(_lastX, _lastY, x, y);
        }
        else
        {
            return IntRect.Empty;
        }

        _lastX = x;
        _lastY = y;
        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            _stroke = null;
        }

        return dirty;
    }
}
