using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC31 for the artistic brushes (ticket 28): the same stroke as <see cref="BrushBenchmarks"/>
/// (4K document, 20 layers, down, 20 moves 10 px apart, up, through the editor) at the largest
/// size, once per brush; the result is the mean cost per pointer event.
/// </summary>
public class ArtisticBrushBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;

    private DocumentEditor _editor = null!;

    [Params("calligraphy-brush", "calligraphy-pen", "crayon", "marker", "natural-pencil")]
    public string Brush { get; set; } = "";

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        var tip = Brush switch
        {
            "calligraphy-brush" => BrushTip.CalligraphyBrush,
            "calligraphy-pen" => BrushTip.CalligraphyPen,
            "crayon" => BrushTip.Crayon,
            "marker" => BrushTip.Marker,
            _ => BrushTip.NaturalPencil,
        };
        _editor = new DocumentEditor(document) { ActiveTool = new ArtisticBrushTool(new StrokeSettings(StrokeSettings.MaxSize), tip) };
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
