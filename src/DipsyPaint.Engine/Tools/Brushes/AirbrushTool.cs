namespace DipsyPaint.Engine.Tools.Brushes;

/// <summary>
/// Reference Paint's airbrush: sprays single-pixel dots at random within a disc of
/// <see cref="StrokeSettings.Size"/> pixels diameter around the pointer, at a steady rate in time,
/// so the paint thickens the longer the pointer stays and is sparse where it moves fast. It keeps
/// spraying while the pointer is held still (<see cref="IRepeatingTool"/>). Each dot covers its
/// pixel fully at <see cref="StrokeSettings.Opacity"/>; dots landing on a pixel already sprayed in the
/// same stroke add nothing. The spray is seeded from where the stroke starts, and times come from
/// <see cref="PointerInput.Time"/>, so a scripted stroke always sprays the same dots.
/// </summary>
public sealed class AirbrushTool(StrokeSettings settings) : ITool, IRepeatingTool
{
    /// <summary>Longest time one event sprays for, so a stalled pointer does not release a burst.</summary>
    private static readonly TimeSpan MaxElapsed = TimeSpan.FromMilliseconds(250);

    private Stroke? _stroke;
    private ArtisticNoise.Sequence _random;
    private double _radius;
    private double _dotsPerSecond;
    private double _owed;
    private double _lastX;
    private double _lastY;
    private TimeSpan? _lastTime;

    public TimeSpan RepeatInterval => PointerInput.NominalInterval;

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = Math.Floor(input.X) + 0.5;
        var y = Math.Floor(input.Y) + 0.5;

        TimeSpan elapsed;
        if (input.Action == PointerAction.Down)
        {
            var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
            _stroke = new Stroke(context.Target, color, settings.OpacityFraction);
            _random = new ArtisticNoise.Sequence(ArtisticNoise.Seed(x, y));
            _radius = settings.Size / 2.0;
            _dotsPerSecond = DotsPerSecond(_radius);
            _owed = 0;
            (_lastX, _lastY) = (x, y);
            elapsed = PointerInput.NominalInterval; // a press sprays straight away
        }
        else if (_stroke is null)
        {
            return IntRect.Empty;
        }
        else
        {
            elapsed = input.Time is { } now && _lastTime is { } last ? now - last : PointerInput.NominalInterval;
        }

        _lastTime = input.Time;
        Spray(_stroke, _lastX, _lastY, x, y, Math.Clamp(elapsed.TotalSeconds, 0, MaxElapsed.TotalSeconds));
        (_lastX, _lastY) = (x, y);
        var dirty = _stroke.TakeDirty();
        if (input.Action == PointerAction.Up)
        {
            _stroke = null;
        }

        return dirty;
    }

    /// <summary>
    /// The spray rate for a tip of <paramref name="radius"/>: about two dots per second for every
    /// pixel of the tip's area, so holding still fills about 86% of the disc in a second at any size,
    /// plus a few so the smallest tips still spray.
    /// </summary>
    private static double DotsPerSecond(double radius) => 2 * Math.PI * radius * radius + 30;

    /// <summary>Sprays the dots due for <paramref name="seconds"/>, spread evenly along the segment the pointer moved.</summary>
    private void Spray(Stroke stroke, double x0, double y0, double x1, double y1, double seconds)
    {
        _owed += _dotsPerSecond * seconds;
        var dots = (int)_owed;
        _owed -= dots;
        for (var i = 0; i < dots; i++)
        {
            var t = _random.Next();
            double u, v;
            do
            {
                u = 2 * _random.Next() - 1;
                v = 2 * _random.Next() - 1;
            }
            while (u * u + v * v > 1);

            stroke.Plot(
                (int)Math.Floor(x0 + t * (x1 - x0) + u * _radius),
                (int)Math.Floor(y0 + t * (y1 - y0) + v * _radius));
        }
    }
}
