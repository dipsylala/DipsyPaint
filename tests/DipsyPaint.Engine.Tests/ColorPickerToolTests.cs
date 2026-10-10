using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class ColorPickerToolTests
{
    private static readonly Rgba Red = new(237, 28, 36, 255);

    private static DocumentEditor CreateEditor(Document document) => new(document) { ActiveTool = new ColorPickerTool() };

    [Fact]
    public void LeftClick_PicksAnOpaquePixelIntoPrimary()
    {
        var document = Document.CreateBlank(8, 8);
        document.ActiveLayer.Pixels.SetPixel(3, 5, Red);
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Left, (3.7, 5.2));

        Assert.Equal(Red, editor.PrimaryColor);
        Assert.Equal(Rgba.White, editor.SecondaryColor);
    }

    [Fact]
    public void RightClick_PicksIntoSecondary()
    {
        var document = Document.CreateBlank(8, 8);
        document.ActiveLayer.Pixels.SetPixel(1, 1, Red);
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Right, (1, 1));

        Assert.Equal(Red, editor.SecondaryColor);
        Assert.Equal(Rgba.Black, editor.PrimaryColor);
    }

    [Fact]
    public void SemiTransparentPixel_IsPickedAsItsOpaqueStraightColour()
    {
        var document = Document.CreateBlank(4, 4);
        document.ActiveLayer.Pixels.SetPixel(2, 2, new Rgba(51, 10, 0, 51)); // straight (255, 50, 0) at 20%
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Left, (2, 2));

        Assert.Equal(new Rgba(255, 50, 0, 255), editor.PrimaryColor);
    }

    [Fact]
    public void PicksTheCompositeOfAllLayers()
    {
        var document = Document.CreateBlank(4, 4);
        document.AddLayer().Pixels.SetPixel(0, 0, new Rgba(0, 0, 128, 128)); // half-transparent blue over white
        document.ActiveLayer = document.Layers[0];
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Left, (0, 0));

        Assert.Equal(new Rgba(127, 127, 255, 255), editor.PrimaryColor);
    }

    [Fact]
    public void Drag_FollowsThePointer()
    {
        var document = Document.CreateBlank(8, 1);
        document.ActiveLayer.Pixels.SetPixel(6, 0, Red);
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Left, (1, 0), (6, 0));

        Assert.Equal(Red, editor.PrimaryColor);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 0)] // fully transparent
    public void TransparentOrOffCanvasPixel_LeavesTheColourUnchanged(double x, double y)
    {
        var document = Document.CreateBlank(4, 4);
        document.ActiveLayer.Pixels.SetPixel(0, 0, Rgba.Transparent);
        var editor = CreateEditor(document);

        Script.Stroke(editor, PointerButton.Left, (x, y));

        Assert.Equal(Rgba.Black, editor.PrimaryColor);
    }

    [Fact]
    public void Picking_ChangesNoPixelsAndRecordsNoHistoryStep()
    {
        var document = Document.CreateBlank(4, 4);
        document.ActiveLayer.Pixels.SetPixel(1, 1, Red);
        var editor = CreateEditor(document);
        var composited = 0;
        editor.CompositeChanged += (_, _) => composited++;

        Script.Stroke(editor, PointerButton.Left, (1, 1), (2, 2));

        Assert.False(editor.History.CanUndo);
        Assert.Equal(0, composited);
        Assert.Equal(Red, document.ActiveLayer.Pixels.GetPixel(1, 1));
    }

    [Fact]
    public void Picking_RaisesColorsChangedAndStrokeEnded()
    {
        var document = Document.CreateBlank(4, 4);
        document.ActiveLayer.Pixels.SetPixel(1, 1, Red);
        var editor = CreateEditor(document);
        var events = new List<string>();
        editor.ColorsChanged += (_, _) => events.Add("colors");
        editor.StrokeEnded += (_, _) => events.Add("ended");

        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(["colors", "ended"], events);
    }

    [Fact]
    public void PencilDrawsWithAPickedColour()
    {
        var document = Document.CreateBlank(8, 8);
        document.ActiveLayer.Pixels.SetPixel(0, 0, Red);
        var editor = CreateEditor(document);
        Script.Stroke(editor, PointerButton.Left, (0, 0));

        editor.ActiveTool = new PencilTool();
        Script.Stroke(editor, PointerButton.Left, (2, 4), (5, 4));

        Assert.All(Enumerable.Range(2, 4), x => Assert.Equal(Red, document.ActiveLayer.Pixels.GetPixel(x, 4)));
    }
}
