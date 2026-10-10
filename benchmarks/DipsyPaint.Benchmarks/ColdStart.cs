using System.Diagnostics;
using System.Globalization;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC33: time from process start to the main window's first rendered frame. The app's startup
/// probe (src/DipsyPaint/StartupProbe.cs), enabled by an environment variable naming a file,
/// appends <c>startup_ms=N …</c> to that file after the first frame and exits. BenchmarkDotNet
/// does not fit process start, so this launches the app several times and reports the median.
/// </summary>
internal static class ColdStart
{
    public const double TargetMilliseconds = 2000;

    // Must match StartupProbe.Variable in src/DipsyPaint/StartupProbe.cs.
    private const string ProbeVariable = "DIPSYPAINT_STARTUP_PROBE";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Launches <paramref name="app"/> (an executable, or a .dll run with <c>dotnet</c>) <paramref name="runs"/> times.</summary>
    public static Measurement Measure(string app, int runs)
    {
        var times = new List<double>();
        for (var run = 1; run <= runs; run++)
        {
            var milliseconds = Launch(Path.GetFullPath(app));
            Console.WriteLine($"Run {run}: {milliseconds:F0} ms");
            times.Add(milliseconds);
        }

        times.Sort();
        var median = runs % 2 == 1 ? times[runs / 2] : (times[runs / 2 - 1] + times[runs / 2]) / 2;
        Console.WriteLine($"Median of {runs}: {median:F0} ms (target {TargetMilliseconds:F0} ms)");
        return new Measurement("ColdStart.FirstFrame", median, TargetMilliseconds);
    }

    private static double Launch(string app)
    {
        var probe = Path.GetTempFileName();
        try
        {
            var start = new ProcessStartInfo { UseShellExecute = false };
            if (app.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                start.FileName = "dotnet";
                start.ArgumentList.Add(app);
            }
            else
            {
                start.FileName = app;
            }

            start.Environment[ProbeVariable] = probe;
            using (var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {app}."))
            {
                if (!process.WaitForExit(Timeout))
                {
                    process.Kill(entireProcessTree: true);
                    throw new InvalidOperationException($"{app} did not exit within {Timeout.TotalSeconds:F0} s.");
                }
            }

            // e.g. "startup_ms=1234 codecs=ok"
            var line = File.ReadAllText(probe).Trim();
            var value = line.Split(' ').FirstOrDefault(part => part.StartsWith("startup_ms=", StringComparison.Ordinal));
            return value is null
                ? throw new InvalidOperationException($"{app} did not report its first frame (probe output: '{line}').")
                : double.Parse(value["startup_ms=".Length..], CultureInfo.InvariantCulture);
        }
        finally
        {
            File.Delete(probe);
        }
    }
}
