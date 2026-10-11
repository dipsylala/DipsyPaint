using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class StrokeOpacityTests
{
    private static readonly Rgba HalfGrey = new(127, 127, 127, 255);

    private static DocumentEditor CreateEditor(ITool tool) => new(Document.CreateBlank(32, 32)) { ActiveTool = tool };

    private static StrokeSettings Settings(int size, int opacity) => new(size) { Opacity = opacity };

    private static Rgba Pixel(DocumentEditor editor, int x, int y) => editor.Document.ActiveLayer.Pixels.GetPixel(x, y);

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    public static TheoryData<string> Tools => ["pencil", "brush", "eraser"];

    private static ITool CreateTool(string name, StrokeSettings settings) => name switch
    {
        "pencil" => new PencilTool(settings),
        "brush" => new BrushTool(settings),
        _ => new EraserTool(settings),
    };

    [Fact]
    public void Opacity_DefaultsToFullAndIsClampedToTheSupportedRange()
    {
        var settings = new StrokeSettings(5);
        Assert.Equal(StrokeSettings.MaxOpacity, settings.Opacity);
        Assert.Equal(1f, settings.OpacityFraction);

        settings.Opacity = 0;
        Assert.Equal(StrokeSettings.MinOpacity, settings.Opacity);

        settings.Opacity = 250;
        Assert.Equal(StrokeSettings.MaxOpacity, settings.Opacity);
    }

    [Fact]
    public void Pencil_PaintsAtTheSetOpacity()
    {
        var editor = CreateEditor(new PencilTool(Settings(1, 50)));

        Script.Stroke(editor, PointerButton.Left, (2, 2), (10, 2));

        Assert.All(Enumerable.Range(2, 9), x => Assert.Equal(HalfGrey, Pixel(editor, x, 2)));
    }

    [Fact]
    public void Brush_PaintsAtTheSetOpacity_AndItsRimStaysLighter()
    {
        var editor = CreateEditor(new BrushTool(Settings(10, 50)));

        Script.Stroke(editor, PointerButton.Left, (16, 16));

        Assert.Equal(HalfGrey, Pixel(editor, 16, 16));
        var rim = Pixel(editor, 16, 11);
        Assert.InRange(rim.R, (byte)128, (byte)254);
    }

    [Fact]
    public void Eraser_LeavesPixelsPartiallyTransparent()
    {
        var editor = CreateEditor(new EraserTool(Settings(4, 50)));

        Script.Stroke(editor, PointerButton.Left, (16, 16));

        // Premultiplied: white at half alpha.
        Assert.Equal(new Rgba(128, 128, 128, 128), Pixel(editor, 16, 16));
        Assert.Equal(Rgba.White, Pixel(editor, 20, 16));
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void GoingOverTheSameStrokeAgain_DoesNotBuildUpOpacity(string tool)
    {
        var once = CreateEditor(CreateTool(tool, Settings(6, 40)));
        var backAndForth = CreateEditor(CreateTool(tool, Settings(6, 40)));

        Script.Stroke(once, PointerButton.Left, (4, 4), (28, 28));
        Script.Stroke(backAndForth, PointerButton.Left, (4, 4), (28, 28), (4, 4), (28, 28), (16, 16));

        Assert.Equal(Snapshot(once), Snapshot(backAndForth));
    }

    [Fact]
    public void SeparateStrokes_BuildUp()
    {
        var pencil = CreateEditor(new PencilTool(Settings(1, 50)));
        Script.Stroke(pencil, PointerButton.Left, (2, 2));
        Script.Stroke(pencil, PointerButton.Left, (2, 2));
        Assert.Equal(new Rgba(63, 63, 63, 255), Pixel(pencil, 2, 2));

        var eraser = CreateEditor(new EraserTool(Settings(1, 50)));
        Script.Stroke(eraser, PointerButton.Left, (2, 2));
        Script.Stroke(eraser, PointerButton.Left, (2, 2));
        Assert.Equal(64, Pixel(eraser, 2, 2).A);
    }

    [Fact]
    public void OpacityChange_AppliesFromTheNextStroke()
    {
        var settings = Settings(1, 50);
        var editor = CreateEditor(new PencilTool(settings));

        editor.Apply(new PointerInput(PointerAction.Down, 2, 2, PointerButton.Left));
        settings.Opacity = 100;
        editor.Apply(new PointerInput(PointerAction.Move, 4, 2, PointerButton.Left));
        editor.Apply(new PointerInput(PointerAction.Up, 4, 2, PointerButton.Left));
        Script.Stroke(editor, PointerButton.Left, (2, 6));

        Assert.Equal(HalfGrey, Pixel(editor, 4, 2));
        Assert.Equal(Rgba.Black, Pixel(editor, 2, 6));
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void TranslucentStrokes_UndoExactly(string tool)
    {
        var editor = CreateEditor(new BrushTool(Settings(12, 100)));
        Script.Stroke(editor, PointerButton.Left, (8, 8), (24, 24));
        var before = Snapshot(editor);
        editor.ActiveTool = CreateTool(tool, Settings(7, 30));

        Script.Stroke(editor, PointerButton.Left, (24, 8), (8, 24));
        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
    }
}
