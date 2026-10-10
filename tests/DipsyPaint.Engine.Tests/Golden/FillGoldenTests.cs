using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

public class FillGoldenTests
{
    private static readonly Rgba Red = new(237, 28, 36, 255);
    private static readonly Rgba Blue = new(63, 72, 204, 255);
    private static readonly Rgba Yellow = new(255, 242, 0, 255);

    private static DocumentEditor CreateEditor() => new(Document.CreateBlank(48, 32)) { PrimaryColor = Red, SecondaryColor = Blue };

    /// <summary>Draws a black 1 px outline, keeping the editor's colours for the fills.</summary>
    private static void Pencil(DocumentEditor editor, params (double X, double Y)[] points)
    {
        var primary = editor.PrimaryColor;
        editor.PrimaryColor = Rgba.Black;
        editor.ActiveTool = new PencilTool();
        Script.Stroke(editor, PointerButton.Left, points);
        editor.PrimaryColor = primary;
    }

    private static void Fill(DocumentEditor editor, PointerButton button, double x, double y)
    {
        editor.ActiveTool = new FillTool();
        Script.Stroke(editor, button, (x, y));
    }

    [Fact]
    public void SimpleRegions()
    {
        var editor = CreateEditor();
        Pencil(editor, (2, 2), (20, 2), (20, 20), (2, 20), (2, 2));        // square
        Pencil(editor, (34, 2), (45, 13), (34, 24), (23, 13), (34, 2));    // diamond: diagonal edges only

        Fill(editor, PointerButton.Left, 10, 10);
        Fill(editor, PointerButton.Right, 34, 13);

        GoldenImage.AssertMatches("fill-simple", editor.Compositor);
    }

    [Fact]
    public void RegionWithHoles()
    {
        var editor = CreateEditor();
        Pencil(editor, (1, 1), (46, 1), (46, 30), (1, 30), (1, 1));        // outer border
        Pencil(editor, (5, 5), (12, 5), (12, 12), (5, 12), (5, 5));        // square hole
        Pencil(editor, (20, 4), (26, 10), (20, 16), (14, 10), (20, 4));    // diamond hole
        Pencil(editor, (30, 4), (42, 4), (42, 26), (30, 26), (30, 8), (38, 8), (38, 22), (34, 22), (34, 12)); // spiral
        Pencil(editor, (6, 20), (24, 27));                                  // stray line inside the region
        Fill(editor, PointerButton.Left, 3, 28);                            // the region around the holes
        Fill(editor, PointerButton.Right, 8, 8);                            // inside one hole

        GoldenImage.AssertMatches("fill-holes", editor.Compositor);
    }

    [Fact]
    public void WholeCanvas()
    {
        var editor = CreateEditor();
        editor.PrimaryColor = Yellow;
        Pencil(editor, (4, 4), (40, 28), (44, 6));                          // open shape: encloses nothing

        Fill(editor, PointerButton.Left, 0, 31);

        GoldenImage.AssertMatches("fill-whole-canvas", editor.Compositor);
    }
}
