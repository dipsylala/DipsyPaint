using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC31 for the artistic brushes (tickets 28 and 29): the same stroke as <see cref="BrushBenchmarks"/>
/// (4K document, 20 layers, down, 20 moves 10 px apart, up, through the editor) at the largest
/// size, once per brush; the result is the mean cost per pointer event. The airbrush sprays for the
/// nominal 16 ms between events that carry no time.
/// </summary>
public class ArtisticBrushBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;

    private DocumentEditor _editor = null!;

    [Params("calligraphy-brush", "calligraphy-pen", "airbrush", "oil-brush", "crayon", "marker", "natural-pencil", "watercolour-brush")]
    public string Brush { get; set; } = "";

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        var settings = new StrokeSettings(StrokeSettings.MaxSize);
        ITool tool = Brush switch
        {
            "calligraphy-brush" => new ArtisticBrushTool(settings, BrushTip.CalligraphyBrush),
            "calligraphy-pen" => new ArtisticBrushTool(settings, BrushTip.CalligraphyPen),
            "airbrush" => new AirbrushTool(settings),
            "oil-brush" => new OilBrushTool(settings),
            "crayon" => new ArtisticBrushTool(settings, BrushTip.Crayon),
            "marker" => new ArtisticBrushTool(settings, BrushTip.Marker),
            "natural-pencil" => new ArtisticBrushTool(settings, BrushTip.NaturalPencil),
            _ => new WatercolourBrushTool(settings),
        };
        _editor = new DocumentEditor(document) { ActiveTool = tool };
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
