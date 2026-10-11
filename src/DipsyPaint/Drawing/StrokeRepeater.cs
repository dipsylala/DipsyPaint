using Avalonia.Threading;

namespace DipsyPaint.Drawing;

/// <summary>
/// While a stroke of an <see cref="Engine.Tools.IRepeatingTool"/> is held, calls back on the UI
/// thread every interval, so the canvas can repeat the last pointer input (e.g. the airbrush keeps
/// spraying while the pointer is still). Timers come from a <see cref="TimeProvider"/>, so tests can
/// drive them with a manual clock.
/// </summary>
internal sealed class StrokeRepeater
{
    private ITimer? _timer;

    public void Start(TimeProvider clock, TimeSpan interval, Action repeat)
    {
        Stop();
        ITimer? timer = null;
        // A tick already posted when the stroke ends is dropped: it belongs to a timer no longer current.
        timer = clock.CreateTimer(_ => Dispatcher.UIThread.Post(() =>
        {
            if (_timer == timer)
            {
                repeat();
            }
        }), null, interval, interval);
        _timer = timer;
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
