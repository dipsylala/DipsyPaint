using System.Text.Json;

namespace DipsyPaint.Benchmarks;

/// <summary>One benchmark result and its ADR-0018 target, as stored in the JSON results files.</summary>
internal sealed record Measurement(string Name, double Milliseconds, double TargetMilliseconds)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<Measurement> Load(string path) =>
        JsonSerializer.Deserialize<List<Measurement>>(File.ReadAllText(path), Options) ?? [];

    public static void Save(string path, IEnumerable<Measurement> measurements)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(measurements, Options));
    }
}
