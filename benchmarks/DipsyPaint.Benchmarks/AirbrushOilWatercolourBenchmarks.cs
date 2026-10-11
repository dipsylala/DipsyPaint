using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC31 for the airbrush, oil brush and watercolour brush (ticket 29): the same stroke as
/// <see cref="BrushBenchmarks"/> (4K document, 20 layers, top layer, down, 20 moves 10 px apart, up,
/// through the editor), reported as the mean cost per pointer event. The airbrush sprays for the
/// nominal 16 ms between events that carry no time.
/// </summary>
public class AirbrushOilWatercolourBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;

    private DocumentEditor _editor = null!;

    public enum BrushKind
    {
        Airbrush,
        Oil,
        Watercolour,
    }

    [Params(BrushKind.Airbrush, BrushKind.Oil, BrushKind.Watercolour)]
    public BrushKind Brush { get; set; }

    /// <summary>The shell's default brush size, and the largest.</summary>
    [Params(5, 100)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        var settings = new StrokeSettings(Size);
        _editor = new DocumentEditor(document)
        {
            ActiveTool = Brush switch
            {
                BrushKind.Airbrush => new AirbrushTool(settings),
                BrushKind.Oil => new OilBrushTool(settings),
                _ => new WatercolourBrushTool(settings),
            },
        };
    }

    [Benchmark(OperationsPerInvoke = Events)]
    [Target(16)]
    public void StrokeEvent()
    {
        const double x = 1500, y = 1000;
        _editor.Apply(new PointerInput(PointerAction.Down, x, y));
        for (var i = 1; i <= Moves; i++)
        {
            _editor.Apply(new PointerInput(PointerAction.Move, x + i * 10, y + i * 3));
        }

        _editor.Apply(new PointerInput(PointerAction.Up, x + Moves * 10, y + Moves * 3));
    }
}
