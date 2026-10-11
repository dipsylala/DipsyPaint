namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// A brush that sweeps a <see cref="TexturedTip"/> of <see cref="StrokeSettings.Size"/> pixels
/// along the pointer path at <see cref="StrokeSettings.Opacity"/>: the left button paints in the
/// primary colour, the right in the secondary, centred on the pixel under the pointer. The tip's
/// texture follows the path, so nothing is painted until the pointer moves or is released; a click
/// leaves one dab. Each stroke's texture is seeded from where it starts, so it is reproducible.
/// </summary>
public abstract class TexturedBrushTool(StrokeSettings settings) : ITool
{
    private PassStroke? _stroke;
    private TexturedTip? _tip;
    private bool _painted;
    private double _startX;
    private double _startY;
    private double _lastX;
    private double _lastY;

    // The texture follows a "string" pulled behind the pointer, so its streaks run smoothly even
    // when the pointer moves in single-pixel steps. The string's end trails the pointer by _lag.
    private double _lag;
    private double _stringX;
    private double _stringY;
    private double _stringAlong;
    private (double X, double Y)? _stringDirection;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = Math.Floor(input.X) + 0.5;
        var y = Math.Floor(input.Y) + 0.5;

        if (input.Action == PointerAction.Down)
        {
            var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
            var radius = settings.Size / 2.0;
            _tip = CreateTip(radius, ArtisticNoise.Seed(x, y));
            _stroke = new PassStroke(context.Target, color, settings.OpacityFraction, NewPassAfter(radius));
            _lag = Math.Max(3, 0.75 * radius);
            (_startX, _startY, _lastX, _lastY, _stringX, _stringY) = (x, y, x, y, x, y);
            (_stringAlong, _stringDirection, _painted) = (0, null, false);
            return IntRect.Empty;
        }

        if (_stroke is null || _tip is null)
        {
            return IntRect.Empty;
        }

        if (x != _lastX || y != _lastY || (input.Action == PointerAction.Up && !_painted))
        {
            TexturedPath.Segment(_stroke, _tip, _lastX, _lastY, x, y, PullString(x, y));
            (_lastX, _lastY, _painted) = (x, y, true);
        }

        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            (_stroke, _tip) = (null, null);
        }

        return dirty;
    }

    /// <summary>The tip for one stroke.</summary>
    protected abstract TexturedTip CreateTip(double radius, ulong seed);

    /// <summary>How much path after first covering a pixel the brush may cover it again and build up (see <see cref="PassStroke"/>).</summary>
    protected abstract double NewPassAfter(double radius);

    /// <summary>
    /// Pulls the string's end towards the pointer at (x, y) and returns the texture frame along it.
    /// Until the string first moves, the texture runs from the stroke's start towards the pointer.
    /// </summary>
    private TextureFrame PullString(double x, double y)
    {
        double dx = x - _stringX, dy = y - _stringY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > _lag)
        {
            var pull = distance - _lag;
            _stringDirection = (dx / distance, dy / distance);
            _stringX += _stringDirection.Value.X * pull;
            _stringY += _stringDirection.Value.Y * pull;
            _stringAlong += pull;
        }

        return _stringDirection is var (ux, uy)
            ? new TextureFrame(_stringX, _stringY, ux, uy, _stringAlong)
            : TextureFrame.ForSegment(_startX, _startY, x, y, 0);
    }
}
