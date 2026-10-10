using System.Text;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// Compares results with their ADR-0018 targets and with a previous run, builds a Markdown table
/// and prints a GitHub Actions warning for each miss. Never fails: regressions are flagged, not
/// blocking (ADR-0018).
/// </summary>
internal static class Report
{
    /// <summary>Slower than the previous run by more than this factor counts as a regression; CI runners are noisy.</summary>
    public const double RegressionFactor = 1.25;

    public static string Build(IReadOnlyList<Measurement> current, IReadOnlyList<Measurement> previous)
    {
        var table = new StringBuilder()
            .AppendLine("## Benchmarks (ADR-0018)")
            .AppendLine()
            .AppendLine("| Benchmark | Result | Target | Previous main run | Status |")
            .AppendLine("| --- | ---: | ---: | ---: | --- |");

        foreach (var measurement in current)
        {
            var before = previous.FirstOrDefault(p => p.Name == measurement.Name);
            var status = new List<string>();
            if (measurement.Milliseconds > measurement.TargetMilliseconds)
            {
                status.Add("over target");
                Warn("Benchmark over target", $"{measurement.Name}: {Ms(measurement.Milliseconds)}, target {Ms(measurement.TargetMilliseconds)}");
            }

            if (before is not null && measurement.Milliseconds > before.Milliseconds * RegressionFactor)
            {
                status.Add("regression");
                Warn("Benchmark regression", $"{measurement.Name}: {Ms(measurement.Milliseconds)}, was {Ms(before.Milliseconds)} on the previous main run");
            }

            var was = before is null ? "–" : $"{Ms(before.Milliseconds)} ({(measurement.Milliseconds / before.Milliseconds - 1) * 100:+0;-0;0}%)";
            table.AppendLine($"| {measurement.Name} | {Ms(measurement.Milliseconds)} | {Ms(measurement.TargetMilliseconds)} | {was} | {(status.Count == 0 ? "ok" : string.Join(", ", status))} |");
        }

        return table.AppendLine()
            .AppendLine($"A regression is more than {(RegressionFactor - 1) * 100:0}% slower than the previous main run. "
                + "Shared CI runners are not reference hardware; see docs/benchmarks.md.")
            .ToString();
    }

    private static string Ms(double milliseconds) => $"{milliseconds:0.###} ms";

    private static void Warn(string title, string message) => Console.WriteLine($"::warning title={title}::{message}");
}
