using System.Globalization;
using System.Reflection;
using BenchmarkDotNet.Running;
using DipsyPaint.Benchmarks;

// Usage (docs/benchmarks.md):
//   [BenchmarkDotNet options]                       engine suites; also writes measurements.json to the results folder
//   coldstart <app> [--runs N] [--out file]         launches the app N times, reports the median time to first frame
//   report [--previous file] [--summary file] [--out file] <results.json>...
//                                                   compares results with targets and a previous run, never fails
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

switch (args.FirstOrDefault())
{
    case "coldstart":
    {
        var measurement = ColdStart.Measure(args[1], int.Parse(Option("--runs") ?? "10"));
        if (Option("--out") is { } path)
        {
            Measurement.Save(path, [measurement]);
        }

        break;
    }

    case "report":
    {
        var current = Inputs().SelectMany(Measurement.Load).ToList();
        var previous = Option("--previous") is { } before && File.Exists(before) ? Measurement.Load(before) : [];
        var markdown = Report.Build(current, previous);
        Console.WriteLine(markdown);
        if (Option("--summary") is { } summary)
        {
            File.AppendAllText(summary, markdown);
        }

        if (Option("--out") is { } path)
        {
            Measurement.Save(path, current);
        }

        break;
    }

    default:
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args).ToList();
        var measurements = summaries
            .SelectMany(s => s.Reports)
            .Where(r => r.ResultStatistics is not null)
            .Select(r =>
            {
                var descriptor = r.BenchmarkCase.Descriptor;
                var parameters = r.BenchmarkCase.HasParameters ? $" {r.BenchmarkCase.Parameters.DisplayInfo}" : "";
                return new Measurement(
                    $"{descriptor.Type.Name.Replace("Benchmarks", "")}.{descriptor.WorkloadMethod.Name}{parameters}",
                    r.ResultStatistics!.Mean / 1e6,
                    descriptor.WorkloadMethod.GetCustomAttribute<TargetAttribute>()?.Milliseconds
                        ?? throw new InvalidOperationException($"{descriptor.WorkloadMethod.Name} has no [Target]."));
            })
            .ToList();
        if (summaries.Count > 0 && measurements.Count > 0)
        {
            var path = Path.Combine(summaries[0].ResultsDirectoryPath, "measurements.json");
            Measurement.Save(path, measurements);
            Console.WriteLine($"Measurements: {path}");
        }

        break;
    }
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

// The arguments after the command that are neither options nor option values.
IEnumerable<string> Inputs()
{
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            i++;
            continue;
        }

        yield return args[i];
    }
}
