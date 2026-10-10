using Avalonia.Threading;

namespace DipsyPaint.Diagnostics;

/// <summary>
/// Writes a <see cref="CrashLog"/> for unhandled exceptions: on the UI thread (before the stack unwinds),
/// anywhere in the process, and in unobserved tasks. It changes no crash behaviour: the app still exits.
/// </summary>
public sealed class CrashHandler(CrashLog log)
{
    private readonly Lock _lock = new();
    private Exception? _last;

    /// <summary>The handler <see cref="Install"/> hooked up, if any.</summary>
    public static CrashHandler? Installed { get; private set; }

    public CrashLog Log { get; } = log;

    /// <summary>
    /// Raised once per exception, after its crash log is written. Handlers run on the crashing thread
    /// (the UI thread for UI exceptions); one that throws is ignored. Hook for the emergency recovery
    /// snapshot (ADR-0013).
    /// </summary>
    public event Action<Exception>? Crashed;

    /// <summary>Hooks this handler up to the process. Call once, after Avalonia's setup.</summary>
    public void Install()
    {
        Installed = this;
        Dispatcher.UIThread.UnhandledException += (_, e) => Handle(e.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Handle(exception, "AppDomain");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => Handle(e.Exception, "Unobserved task");
    }

    /// <summary>Logs <paramref name="exception"/> and raises <see cref="Crashed"/>; does nothing for the same exception twice.</summary>
    public void Handle(Exception exception, string source)
    {
        lock (_lock)
        {
            // A UI-thread exception reaches the AppDomain handler too.
            if (ReferenceEquals(exception, _last))
            {
                return;
            }

            _last = exception;
            try
            {
                Log.Write(exception, source, DateTimeOffset.UtcNow);
            }
            catch
            {
                // Never let crash logging hide the original exception.
            }

            foreach (var handler in Crashed?.GetInvocationList().Cast<Action<Exception>>() ?? [])
            {
                try
                {
                    handler(exception);
                }
                catch
                {
                    // As above.
                }
            }
        }
    }
}
