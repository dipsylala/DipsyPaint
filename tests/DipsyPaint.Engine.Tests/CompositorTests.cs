using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class CompositorTests
{
    [Fact]
    public void Composites_LayersSourceOverBottomFirst()
    {
        var document = Document.CreateBlank(2, 1);
        document.AddLayer().Pixels.SetPixel(0, 0, new Rgba(0, 0, 128, 128)); // half-transparent blue

        var compositor = new Compositor(document);

        Assert.Equal(new Rgba(127, 127, 255, 255), compositor.Pixels[0]);
        Assert.Equal(Rgba.White, compositor.Pixels[1]);
    }

    [Fact]
    public void Update_RecompositesOnlyTheDirtyRectangle()
    {
        var document = Document.CreateBlank(4, 1);
        var compositor = new Compositor(document);
        document.ActiveLayer.Pixels.SetPixel(1, 0, Rgba.Black);
        document.ActiveLayer.Pixels.SetPixel(3, 0, Rgba.Black);

        compositor.Update(new IntRect(1, 0, 1, 1));

        Assert.Equal([Rgba.White, Rgba.Black, Rgba.White, Rgba.White], compositor.Pixels.ToArray());
    }

    [Fact]
    public void Update_IgnoresTheOffCanvasPart()
    {
        var document = Document.CreateBlank(2, 2);
        var compositor = new Compositor(document);
        document.ActiveLayer.Pixels.SetPixel(1, 1, Rgba.Black);

        compositor.Update(new IntRect(-5, -5, 20, 20));

        Assert.Equal(Rgba.Black, compositor.Pixels[3]);
    }

    [Fact]
    public void Editor_UpdatesCompositeAndReportsChange()
    {
        var editor = new DocumentEditor(Document.CreateBlank(8, 8)) { ActiveTool = new PencilTool() };
        var changes = new List<IntRect>();
        editor.CompositeChanged += (_, rect) => changes.Add(rect);

        Script.Stroke(editor, PointerButton.Left, (2, 3));

        Assert.Equal([new IntRect(2, 3, 1, 1)], changes);
        Assert.Equal(Rgba.Black, editor.Compositor.Pixels[3 * 8 + 2]);
    }

    [Fact]
    public void Editor_WithoutActiveTool_IgnoresInput()
    {
        var editor = new DocumentEditor(Document.CreateBlank(4, 4));

        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
    }
}
