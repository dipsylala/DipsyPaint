using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Compositing several layers, through the editor, as layers are hidden and shown: a white bottom
/// layer, a red rectangle, a blue ellipse overlapping it, and a top layer with a translucent yellow
/// rectangle and black pencil lines.
/// </summary>
public class LayerGoldenTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);
    private static readonly Rgba TranslucentYellow = new(128, 128, 0, 128); // premultiplied

    private static DocumentEditor DrawLayers()
    {
        var shapes = new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = ShapeStyle.None, Fill = ShapeStyle.Solid };
        var editor = new DocumentEditor(Document.CreateBlank(64, 40)) { ActiveTool = new ShapeTool(shapes) };

        editor.AddLayer();
        editor.SecondaryColor = Red;
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (32.5, 28.5));
        editor.CommitPending();

        editor.AddLayer();
        shapes.Kind = ShapeKind.Ellipse;
        editor.SecondaryColor = Blue;
        Script.Stroke(editor, PointerButton.Left, (20.5, 10.5), (56.5, 36.5));
        editor.CommitPending();

        editor.AddLayer();
        shapes.Kind = ShapeKind.Rectangle;
        editor.SecondaryColor = TranslucentYellow;
        Script.Stroke(editor, PointerButton.Left, (12.5, 16.5), (48.5, 24.5));
        editor.CommitPending();
        editor.ActiveTool = new PencilTool();
        Script.Stroke(editor, PointerButton.Left, (2, 36), (60, 2));
        Script.Stroke(editor, PointerButton.Left, (2, 2), (60, 36));
        return editor;
    }

    [Fact]
    public void AllLayersVisible()
    {
        var editor = DrawLayers();

        GoldenImage.AssertMatches("layers-all-visible", editor.Compositor);
        LayerOperationsTests.AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void HidingTheMiddleLayers_ShowsTheLayersBelowThrough()
    {
        var editor = DrawLayers();
        var layers = editor.Document.Layers;

        editor.SetLayerVisible(layers[2], false);
        GoldenImage.AssertMatches("layers-ellipse-hidden", editor.Compositor);

        editor.SetLayerVisible(layers[1], false);
        GoldenImage.AssertMatches("layers-rectangle-and-ellipse-hidden", editor.Compositor);
    }

    [Fact]
    public void HidingTheBottomLayer_LeavesTransparency()
    {
        var editor = DrawLayers();

        editor.SetLayerVisible(editor.Document.Layers[0], false);

        GoldenImage.AssertMatches("layers-bottom-hidden", editor.Compositor);
    }

    [Fact]
    public void ShowingHiddenLayersAgain_RestoresTheComposite_AsDoesUndoingTheHides()
    {
        var editor = DrawLayers();
        var layers = editor.Document.Layers.ToArray();
        foreach (var layer in layers)
        {
            editor.SetLayerVisible(layer, false);
        }

        Assert.All(editor.Compositor.Pixels.ToArray(), p => Assert.Equal(Rgba.Transparent, p));

        foreach (var layer in layers)
        {
            editor.SetLayerVisible(layer, true);
        }

        GoldenImage.AssertMatches("layers-all-visible", editor.Compositor);

        for (var i = 0; i < 2 * layers.Length; i++)
        {
            editor.Undo();
        }

        GoldenImage.AssertMatches("layers-all-visible", editor.Compositor);
    }

    [Fact]
    public void Exports_FlattenTheVisibleLayers()
    {
        var editor = DrawLayers();
        editor.SetLayerVisible(editor.Document.Layers[2], false);

        using var file = new MemoryStream();
        ImageCodec.Export(editor.Compositor, file, ImageFormat.Png);
        file.Position = 0;
        var reopened = ImageCodec.Open(file);

        Assert.Single(reopened.Layers);
        Assert.Equal(editor.Compositor.Pixels.ToArray(), new Compositor(reopened).Pixels.ToArray());
        GoldenImage.AssertMatches("layers-ellipse-hidden", new Compositor(reopened));
    }
}
