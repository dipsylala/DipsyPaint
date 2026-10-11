using DipsyPaint.Engine.Tools;
using DipsyPaint.Engine.Tools.Brushes;

namespace DipsyPaint.Engine.Tests.Golden;

public class ArtisticBrushGoldenTests
{
    public static TheoryData<string> Brushes => ["calligraphy-brush", "calligraphy-pen", "crayon", "marker", "natural-pencil"];

    [Theory]
    [MemberData(nameof(Brushes))]
    public void BrushStrokes(string brush)
    {
        var editor = new DocumentEditor(Document.CreateBlank(128, 96)) { SecondaryColor = new Rgba(0, 0, 255, 255) };
        var tip = TipOf(brush);

        Draw(editor, tip, 1, 100, PointerButton.Left, (4, 4), (60, 4));
        Draw(editor, tip, 3, 100, PointerButton.Left, (4, 10), (60, 14));
        Draw(editor, tip, 8, 100, PointerButton.Left, (6, 24), (58, 24));                         // across the nibs
        Draw(editor, tip, 8, 100, PointerButton.Left, (70, 4), (90, 24), (110, 4));                // along each nib in turn
        Draw(editor, tip, 20, 100, PointerButton.Left, (12, 46));                                  // single dab
        Draw(editor, tip, 20, 100, PointerButton.Right, (34, 40), (60, 60), (34, 80), (34, 40));    // secondary colour, crosses itself
        Draw(editor, tip, 12, 100, PointerButton.Left, Circle(96, 50, 18));                        // every direction
        Draw(editor, tip, 16, 50, PointerButton.Left, (70, 84), (124, 84));                        // half opacity
        Draw(editor, tip, 16, 50, PointerButton.Left, (110, 70), (110, 96));                       // crosses the stroke above
        Draw(editor, tip, 6, 100, PointerButton.Left, (-10, 92), (20, 70));                        // enters from off the canvas

        GoldenImage.AssertMatches("artistic-" + brush, editor.Compositor);
    }

    internal static BrushTip TipOf(string brush) => brush switch
    {
        "calligraphy-brush" => BrushTip.CalligraphyBrush,
        "calligraphy-pen" => BrushTip.CalligraphyPen,
        "crayon" => BrushTip.Crayon,
        "marker" => BrushTip.Marker,
        "natural-pencil" => BrushTip.NaturalPencil,
        _ => throw new ArgumentOutOfRangeException(nameof(brush)),
    };

    /// <summary>
    /// A ring of whole-pixel points from a fixed table rather than Math.Cos, whose last bit may
    /// differ between platforms.
    /// </summary>
    private static (double X, double Y)[] Circle(double cx, double cy, double radius)
    {
        (double X, double Y)[] unit =
        [
            (1, 0), (0.92, 0.38), (0.71, 0.71), (0.38, 0.92), (0, 1), (-0.38, 0.92), (-0.71, 0.71), (-0.92, 0.38),
            (-1, 0), (-0.92, -0.38), (-0.71, -0.71), (-0.38, -0.92), (0, -1), (0.38, -0.92), (0.71, -0.71), (0.92, -0.38), (1, 0),
        ];
        return unit.Select(p => (Math.Round(cx + radius * p.X), Math.Round(cy + radius * p.Y))).ToArray();
    }

    private static void Draw(
        DocumentEditor editor, BrushTip tip, int size, int opacity, PointerButton button, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new ArtisticBrushTool(new StrokeSettings(size) { Opacity = opacity }, tip);
        Script.Stroke(editor, button, points);
    }
}
