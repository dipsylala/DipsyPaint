namespace DipsyPaint.Tests;

/// <summary>A <see cref="TimeProvider"/> whose time moves only when a test calls <see cref="Advance"/>, firing due timers on the way.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<Timer> _timers = [];
    private long _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        var end = _now + by.Ticks;
        while (_timers.Where(t => t.Due <= end).MinBy(t => t.Due) is { } timer)
        {
            _now = Math.Max(_now, timer.Due);
            timer.Fire();
        }

        _now = end;
    }

    private sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _period;

        public long Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock._timers.Remove(this);
            _period = period > TimeSpan.Zero ? period.Ticks : 0;
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                Due = clock._now + dueTime.Ticks;
                clock._timers.Add(this);
            }

            return true;
        }

        public void Fire()
        {
            clock._timers.Remove(this);
            if (_period > 0)
            {
                Due += _period;
                clock._timers.Add(this);
            }

            callback(state);
        }

        public void Dispose() => clock._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
