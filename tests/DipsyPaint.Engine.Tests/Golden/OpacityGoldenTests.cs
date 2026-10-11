using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Pencil, brush and eraser strokes at several opacities. Each image also has a stroke that goes over
/// itself (no build-up within one stroke) and two separate strokes that cross (they build up).
/// </summary>
public class OpacityGoldenTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);
    private static readonly int[] Opacities = [100, 75, 50, 25, 10];

    [Fact]
    public void PencilOpacity()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64)) { PrimaryColor = Red, SecondaryColor = Blue };
        Brush(editor, 8, 100, PointerButton.Left, (46, 2), (46, 20)); // red band underneath, to blend over

        for (var i = 0; i < Opacities.Length; i++)
        {
            var y = 4 + i * 4;
            Pencil(editor, Opacities[i], PointerButton.Left, (4, y), (90, y));
        }

        Pencil(editor, 50, PointerButton.Right, (4, 28), (40, 60), (40, 28), (4, 60), (22, 28)); // crosses itself
        Pencil(editor, 50, PointerButton.Left, (50, 30), (90, 60));                              // two separate
        Pencil(editor, 50, PointerButton.Left, (50, 60), (90, 30));                              // strokes cross

        GoldenImage.AssertMatches("pencil-opacity", editor.Compositor);
    }

    [Fact]
    public void BrushOpacity()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64)) { PrimaryColor = Red, SecondaryColor = Blue };
        Brush(editor, 12, 100, PointerButton.Right, (46, 2), (46, 28)); // blue band underneath, to blend over

        for (var i = 0; i < Opacities.Length; i++)
        {
            var y = 4 + i * 6;
            Brush(editor, 5, Opacities[i], PointerButton.Left, (4, y), (90, y));
        }

        Brush(editor, 6, 40, PointerButton.Left, (6, 36), (40, 60), (40, 36), (6, 60), (23, 36)); // crosses itself
        Brush(editor, 8, 50, PointerButton.Right, (50, 36), (90, 60));                            // two separate
        Brush(editor, 8, 50, PointerButton.Right, (50, 60), (90, 36));                            // strokes cross

        GoldenImage.AssertMatches("brush-opacity", editor.Compositor);
    }

    [Fact]
    public void EraserOpacity()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64)) { PrimaryColor = Blue };
        Brush(editor, 24, 100, PointerButton.Left, (10, 14), (86, 14)); // paint to erase, white gap between
        Brush(editor, 24, 100, PointerButton.Left, (10, 50), (86, 50));

        for (var i = 0; i < Opacities.Length; i++)
        {
            var x = 6 + i * 10;
            Eraser(editor, 6, Opacities[i], (x, 2), (x, 32)); // over paint, then white
        }

        Eraser(editor, 5, 50, (6, 38), (40, 62), (40, 38), (6, 62), (23, 38)); // crosses itself
        Eraser(editor, 8, 50, (50, 38), (90, 62));                             // two separate
        Eraser(editor, 8, 50, (50, 62), (90, 38));                             // strokes cross

        GoldenImage.AssertMatches("eraser-opacity", editor.Compositor);
    }

    private static void Pencil(DocumentEditor editor, int opacity, PointerButton button, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new PencilTool(new StrokeSettings(1) { Opacity = opacity });
        Script.Stroke(editor, button, points);
    }

    private static void Brush(DocumentEditor editor, int size, int opacity, PointerButton button, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new BrushTool(new StrokeSettings(size) { Opacity = opacity });
        Script.Stroke(editor, button, points);
    }

    private static void Eraser(DocumentEditor editor, int size, int opacity, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new EraserTool(new StrokeSettings(size) { Opacity = opacity });
        Script.Stroke(editor, PointerButton.Left, points);
    }
}
