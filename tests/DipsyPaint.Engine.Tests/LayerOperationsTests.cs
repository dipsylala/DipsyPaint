using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class LayerOperationsTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);
    private static readonly Rgba HalfGreen = new(0, 64, 0, 128); // premultiplied

    /// <summary>
    /// A 4×2 document of three layers: white bottom; layer 1 red at (0, 0) and (1, 0); layer 2 (top,
    /// active) blue at (1, 0) and half-transparent green at (2, 1).
    /// </summary>
    private static DocumentEditor ThreeLayers()
    {
        var document = Document.CreateBlank(4, 2);
        var middle = document.AddLayer();
        middle.Pixels.SetPixel(0, 0, Red);
        middle.Pixels.SetPixel(1, 0, Red);
        var top = document.AddLayer();
        top.Pixels.SetPixel(1, 0, Blue);
        top.Pixels.SetPixel(2, 1, HalfGreen);
        return new DocumentEditor(document);
    }

    /// <summary>Composites the document the slow, obvious way: every visible layer, bottom first.</summary>
    internal static Rgba[] ReferenceComposite(Document document)
    {
        var result = new Rgba[document.Width * document.Height];
        foreach (var layer in document.Layers.Where(l => l.IsVisible))
        {
            for (var y = 0; y < document.Height; y++)
            {
                for (var x = 0; x < document.Width; x++)
                {
                    var i = y * document.Width + x;
                    result[i] = layer.Pixels.GetPixel(x, y).WithOpacity((float)layer.Opacity).Over(result[i]);
                }
            }
        }

        return result;
    }

    /// <summary>The composite matches <see cref="ReferenceComposite"/> within 1 per channel (rounding of the cached composites).</summary>
    internal static void AssertCompositeIsCorrect(DocumentEditor editor)
    {
        var expected = ReferenceComposite(editor.Document);
        var actual = editor.Compositor.Pixels.ToArray();
        for (var i = 0; i < expected.Length; i++)
        {
            var (e, a) = (expected[i], actual[i]);
            var difference = Math.Max(Math.Max(Math.Abs(e.R - a.R), Math.Abs(e.G - a.G)), Math.Max(Math.Abs(e.B - a.B), Math.Abs(e.A - a.A)));
            Assert.True(difference <= 1, $"Pixel {i}: expected {e}, got {a}.");
        }
    }

    private static Rgba[] PixelsOf(Layer layer)
    {
        var pixels = new Rgba[layer.Pixels.Width * layer.Pixels.Height];
        layer.Pixels.ReadRegion(new IntRect(0, 0, layer.Pixels.Width, layer.Pixels.Height), pixels);
        return pixels;
    }

    private sealed record State(Layer[] Layers, Layer Active, (bool Visible, double Opacity)[] Properties, Rgba[][] Pixels, Rgba[] Composite);

    private static State Capture(DocumentEditor editor) => new(
        [.. editor.Document.Layers],
        editor.Document.ActiveLayer,
        [.. editor.Document.Layers.Select(l => (l.IsVisible, l.Opacity))],
        [.. editor.Document.Layers.Select(PixelsOf)],
        editor.Compositor.Pixels.ToArray());

    private static void AssertState(State expected, DocumentEditor editor)
    {
        var actual = Capture(editor);
        Assert.Equal(expected.Layers, actual.Layers);
        Assert.Same(expected.Active, actual.Active);
        Assert.Equal(expected.Properties, actual.Properties);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal(expected.Composite, actual.Composite);
    }

    [Fact]
    public void AddLayer_InsertsATransparentLayerAboveTheActiveOne_AndMakesItActive()
    {
        var editor = ThreeLayers();
        var middle = editor.Document.Layers[1];
        editor.SelectLayer(middle);
        var composite = editor.Compositor.Pixels.ToArray();

        Assert.True(editor.AddLayer());

        var added = editor.Document.ActiveLayer;
        Assert.Equal(4, editor.Document.Layers.Count);
        Assert.Same(added, editor.Document.Layers[2]);
        Assert.All(PixelsOf(added), p => Assert.Equal(Rgba.Transparent, p));
        Assert.True(added.IsVisible);
        Assert.Equal(1, added.Opacity);
        Assert.Equal(composite, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void NewLayers_AreDrawnOnAndCompositedInOrder()
    {
        var editor = new DocumentEditor(Document.CreateBlank(4, 4)) { ActiveTool = new PencilTool(), PrimaryColor = Red };
        editor.AddLayer();
        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(Rgba.White, editor.Document.Layers[0].Pixels.GetPixel(1, 1));
        Assert.Equal(Red, editor.Document.Layers[1].Pixels.GetPixel(1, 1));
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void AddLayer_OverTheMemoryBudget_IsRefused_AndChangesNothing()
    {
        var editor = ThreeLayers();
        var before = Capture(editor);
        // 3 layers + the new one + composite, two caches and the screen bitmap: 8 bitmaps of 4×2×4 bytes.
        var justTooSmall = new MemoryBudget(8 * 4 * 2 * 4 - 1);

        Assert.False(editor.AddLayer(justTooSmall));
        Assert.False(editor.DuplicateLayer(editor.Document.ActiveLayer, justTooSmall));

        AssertState(before, editor);
        Assert.False(editor.History.CanUndo);
        Assert.True(editor.AddLayer(new MemoryBudget(8 * 4 * 2 * 4)));
    }

    [Fact]
    public void DuplicateLayer_PutsACopyDirectlyAbove_AndMakesItActive()
    {
        var editor = ThreeLayers();
        var middle = editor.Document.Layers[1];
        editor.SetLayerOpacity(middle, 0.5);

        Assert.True(editor.DuplicateLayer(middle));

        var copy = editor.Document.Layers[2];
        Assert.NotSame(middle, copy);
        Assert.Same(copy, editor.Document.ActiveLayer);
        Assert.Equal(PixelsOf(middle), PixelsOf(copy));
        Assert.Equal(0.5, copy.Opacity);

        // The copy is independent of the original.
        copy.Pixels.SetPixel(3, 1, Red);
        Assert.Equal(Rgba.Transparent, middle.Pixels.GetPixel(3, 1));
    }

    [Theory]
    [InlineData(2, 1)] // the top layer: the one below becomes active
    [InlineData(0, 0)] // the bottom layer: the one above (now the bottom) becomes active
    public void DeleteLayer_RemovesIt_AndANeighbourBecomesActive(int deleted, int newActiveIndex)
    {
        var editor = ThreeLayers();
        var layer = editor.Document.Layers[deleted];
        editor.SelectLayer(layer);

        editor.DeleteLayer(layer);

        Assert.Equal(2, editor.Document.Layers.Count);
        Assert.DoesNotContain(layer, editor.Document.Layers);
        Assert.Same(editor.Document.Layers[newActiveIndex], editor.Document.ActiveLayer);
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void DeleteLayer_OfAnInactiveLayer_KeepsTheActiveLayer()
    {
        var editor = ThreeLayers();
        var top = editor.Document.ActiveLayer;

        editor.DeleteLayer(editor.Document.Layers[1]);

        Assert.Same(top, editor.Document.ActiveLayer);
        Assert.Equal(Blue, editor.Compositor.Pixels[1]);
        Assert.Equal(Rgba.White, editor.Compositor.Pixels[0]); // the red pixel went with its layer
    }

    [Fact]
    public void DeleteLayer_NeverDeletesTheLastLayer()
    {
        var editor = new DocumentEditor(Document.CreateBlank(4, 2));
        var only = editor.Document.ActiveLayer;

        Assert.False(editor.CanDeleteLayer);
        editor.DeleteLayer(only);

        Assert.Equal([only], editor.Document.Layers);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MergeDown_CompositesTheLayerOntoTheOneBelow_AndRemovesIt()
    {
        var editor = ThreeLayers();
        var (middle, top) = (editor.Document.Layers[1], editor.Document.Layers[2]);
        var composite = editor.Compositor.Pixels.ToArray();

        editor.MergeDown(top);

        Assert.Equal(2, editor.Document.Layers.Count);
        Assert.Same(middle, editor.Document.Layers[1]);
        Assert.Same(middle, editor.Document.ActiveLayer);
        Assert.Equal(Red, middle.Pixels.GetPixel(0, 0));
        Assert.Equal(Blue, middle.Pixels.GetPixel(1, 0));
        Assert.Equal(HalfGreen, middle.Pixels.GetPixel(2, 1));
        Assert.Equal(composite, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void MergeDown_OfAHiddenLayer_AddsNothing_AndKeepsTheLowerLayersProperties()
    {
        var editor = ThreeLayers();
        var (middle, top) = (editor.Document.Layers[1], editor.Document.Layers[2]);
        var middlePixels = PixelsOf(middle);
        editor.SetLayerVisible(top, false);
        editor.SetLayerOpacity(middle, 0.25);

        editor.MergeDown(top);

        Assert.Equal(middlePixels, PixelsOf(middle));
        Assert.True(middle.IsVisible);
        Assert.Equal(0.25, middle.Opacity);
    }

    [Fact]
    public void MergeDown_AppliesTheLayersOpacity()
    {
        var editor = ThreeLayers();
        var (middle, top) = (editor.Document.Layers[1], editor.Document.Layers[2]);
        editor.SetLayerOpacity(top, 0.5);
        var composite = editor.Compositor.Pixels.ToArray();

        editor.MergeDown(top);

        Assert.Equal(Blue.WithOpacity(0.5f).Over(Red), middle.Pixels.GetPixel(1, 0));
        Assert.Equal(composite, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void MergeDown_OfTheBottomLayer_DoesNothing()
    {
        var editor = ThreeLayers();
        var bottom = editor.Document.Layers[0];

        Assert.False(editor.CanMergeDown(bottom));
        editor.MergeDown(bottom);

        Assert.Equal(3, editor.Document.Layers.Count);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MergeDown_StepHoldsOnlyTheTilesTheLayerCovers()
    {
        var document = Document.CreateBlank(512, 512);
        document.AddLayer().Pixels.SetPixel(10, 10, Red);
        var editor = new DocumentEditor(document);

        editor.MergeDown(document.ActiveLayer);

        // The removed layer (1 MiB) and one 64×64 tile of the layer below.
        Assert.Equal(512 * 512 * 4 + 64 * 64 * 4, editor.History.SizeBytes);
    }

    [Fact]
    public void HidingALayer_RemovesItFromTheComposite_AndShowingItRestoresIt()
    {
        var editor = ThreeLayers();
        var top = editor.Document.ActiveLayer;
        var shown = editor.Compositor.Pixels.ToArray();

        editor.SetLayerVisible(top, false);

        Assert.False(top.IsVisible);
        Assert.Equal(Red, editor.Compositor.Pixels[1]);
        AssertCompositeIsCorrect(editor);

        editor.SetLayerVisible(top, true);
        Assert.Equal(shown, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void AHiddenLayer_CanStillBeDrawnOn_WithoutShowing()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new PencilTool();
        editor.SetLayerVisible(editor.Document.ActiveLayer, false);
        var composite = editor.Compositor.Pixels.ToArray();

        Script.Stroke(editor, PointerButton.Left, (3, 1));

        Assert.Equal(Rgba.Black, editor.Document.ActiveLayer.Pixels.GetPixel(3, 1));
        Assert.Equal(composite, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void MoveLayer_ReordersTheStack()
    {
        var editor = ThreeLayers();
        var (middle, top) = (editor.Document.Layers[1], editor.Document.Layers[2]);

        editor.MoveLayer(top, 1);

        Assert.Equal([editor.Document.Layers[0], top, middle], editor.Document.Layers);
        Assert.Same(top, editor.Document.ActiveLayer);
        Assert.Equal(Red, editor.Compositor.Pixels[1]); // red is now above blue
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void MoveLayer_ClampsTheIndex_AndIgnoresANonMove()
    {
        var editor = ThreeLayers();
        var top = editor.Document.ActiveLayer;

        editor.MoveLayer(top, 99);
        Assert.False(editor.History.CanUndo);

        editor.MoveLayer(top, -5);
        Assert.Same(top, editor.Document.Layers[0]);
    }

    [Fact]
    public void SelectLayer_ChangesWhereToolsDraw_AndIsNotAHistoryStep()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new PencilTool();
        var middle = editor.Document.Layers[1];
        var changes = 0;
        editor.LayersChanged += (_, _) => changes++;

        editor.SelectLayer(middle);
        Script.Stroke(editor, PointerButton.Left, (3, 0));

        Assert.Equal(1, changes);
        Assert.Equal(Rgba.Black, middle.Pixels.GetPixel(3, 0));
        editor.Undo();
        Assert.Equal(Rgba.Transparent, middle.Pixels.GetPixel(3, 0));
        Assert.Same(middle, editor.Document.ActiveLayer);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DrawingOnALowerLayer_ShowsUnderTheLayersAbove()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new PencilTool();
        editor.PrimaryColor = Red;
        editor.SelectLayer(editor.Document.Layers[0]);

        Script.Stroke(editor, PointerButton.Left, (1, 0), (2, 0), (2, 1), (3, 1));

        Assert.Equal(Blue, editor.Compositor.Pixels[1]);      // covered by the top layer's blue
        Assert.Equal(Red, editor.Compositor.Pixels[2]);       // nothing above
        Assert.Equal(HalfGreen.Over(Red), editor.Compositor.Pixels[4 + 2]);
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void UndoingAStrokeOnAnotherLayer_UpdatesTheComposite()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new PencilTool();
        editor.PrimaryColor = Red;
        editor.SelectLayer(editor.Document.Layers[0]);
        Script.Stroke(editor, PointerButton.Left, (3, 1));
        editor.SelectLayer(editor.Document.Layers[2]);

        editor.Undo();
        Assert.Equal(Rgba.White, editor.Compositor.Pixels[4 + 3]);
        AssertCompositeIsCorrect(editor);

        editor.Redo();
        Assert.Equal(Red, editor.Compositor.Pixels[4 + 3]);
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void PendingContent_IsCommittedToItsLayer_BeforeALayerOperation()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new ShapeTool(new ShapeSettings { Kind = ShapeKind.Line });
        var top = editor.Document.ActiveLayer;
        Script.Stroke(editor, PointerButton.Left, (0.5, 1.5), (1.5, 1.5));
        Assert.NotNull(editor.Adornment);

        editor.SelectLayer(editor.Document.Layers[0]);

        Assert.Null(editor.Adornment);
        Assert.Equal(Rgba.Black, top.Pixels.GetPixel(0, 1));
        Assert.Equal(Rgba.White, editor.Document.Layers[0].Pixels.GetPixel(0, 1));
    }

    [Fact]
    public void LayerOperations_AreIgnoredDuringAGesture()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new PencilTool();
        editor.Apply(new PointerInput(PointerAction.Down, 0, 0));
        var layers = editor.Document.Layers.ToArray();

        editor.AddLayer();
        editor.DeleteLayer(layers[1]);
        editor.MergeDown(layers[2]);
        editor.MoveLayer(layers[2], 0);
        editor.SetLayerVisible(layers[1], false);
        editor.SelectLayer(layers[0]);

        Assert.Equal(layers, editor.Document.Layers);
        Assert.Same(layers[2], editor.Document.ActiveLayer);
        Assert.True(layers[1].IsVisible);
        editor.Apply(new PointerInput(PointerAction.Up, 0, 0));
    }

    [Fact]
    public void ALayerOfAnotherDocument_IsRejected()
    {
        var editor = ThreeLayers();
        var stranger = Document.CreateBlank(4, 2).ActiveLayer;

        Assert.Throws<ArgumentException>(() => editor.DeleteLayer(stranger));
        Assert.Throws<ArgumentException>(() => editor.SelectLayer(stranger));
    }

    public static TheoryData<string> Operations => new() { "add", "duplicate", "delete", "merge", "hide", "move", "opacity" };

    private static void Perform(DocumentEditor editor, string operation)
    {
        var layers = editor.Document.Layers;
        switch (operation)
        {
            case "add": editor.AddLayer(); break;
            case "duplicate": editor.DuplicateLayer(layers[1]); break;
            case "delete": editor.DeleteLayer(layers[2]); break;
            case "merge": editor.MergeDown(layers[2]); break;
            case "hide": editor.SetLayerVisible(layers[1], false); break;
            case "move": editor.MoveLayer(layers[2], 0); break;
            case "opacity": editor.SetLayerOpacity(layers[2], 0.5); break;
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryLayerOperation_IsOneHistoryStep_UndoneAndRedoneExactly(string operation)
    {
        var editor = ThreeLayers();
        var before = Capture(editor);
        var layersChanged = 0;
        editor.LayersChanged += (_, _) => layersChanged++;

        Perform(editor, operation);
        var after = Capture(editor);
        Assert.Equal(1, layersChanged);
        AssertCompositeIsCorrect(editor);

        editor.Undo();
        AssertState(before, editor);
        Assert.False(editor.History.CanUndo);

        editor.Redo();
        AssertState(after, editor);
    }

    [Fact]
    public void UndoingADelete_AfterACanvasResize_IsUndoneInOrder()
    {
        var editor = ThreeLayers();
        var before = Capture(editor);
        editor.DeleteLayer(editor.Document.Layers[1]);
        editor.Resize(8, 4);

        editor.Undo();
        editor.Undo();

        AssertState(before, editor);
    }

    [Fact]
    public void Compositor_WithOneLayerOpacity_BlendsItAtThatOpacity()
    {
        var editor = ThreeLayers();
        editor.SetLayerOpacity(editor.Document.Layers[1], 0.5);

        Assert.Equal(Red.WithOpacity(0.5f).Over(Rgba.White), editor.Compositor.Pixels[0]);
        AssertCompositeIsCorrect(editor);
    }

    [Fact]
    public void PendingContent_OnALayerWithOpacity_IsShownAtThatOpacity()
    {
        var editor = ThreeLayers();
        editor.ActiveTool = new ShapeTool(new ShapeSettings { Kind = ShapeKind.Line });
        editor.SelectLayer(editor.Document.Layers[1]);
        editor.SetLayerOpacity(editor.Document.Layers[1], 0.5);

        Script.Stroke(editor, PointerButton.Left, (2.5, 1.5), (3.5, 1.5));
        var pending = editor.Compositor.Pixels[4 + 3];
        editor.CommitPending();

        Assert.Equal(Rgba.Black.WithOpacity(0.5f).Over(Rgba.White), pending);
        Assert.Equal(pending, editor.Compositor.Pixels[4 + 3]);
    }

    [Fact]
    public void Compositor_StaysCorrectThroughAMixOfOperations()
    {
        var random = new Random(17);
        var document = Document.CreateBlank(24, 16);
        var editor = new DocumentEditor(document) { ActiveTool = new PencilTool() };
        for (var step = 0; step < 200; step++)
        {
            var layers = document.Layers;
            var layer = layers[random.Next(layers.Count)];
            switch (random.Next(9))
            {
                case 0: editor.AddLayer(); break;
                case 1: editor.DuplicateLayer(layer); break;
                case 2: editor.DeleteLayer(layer); break;
                case 3: editor.MergeDown(layer); break;
                case 4: editor.SetLayerVisible(layer, !layer.IsVisible); break;
                case 5: editor.MoveLayer(layer, random.Next(layers.Count)); break;
                case 6: editor.SelectLayer(layer); break;
                case 7: editor.Undo(); break;
                default:
                    editor.PrimaryColor = new Rgba((byte)random.Next(128), 0, (byte)random.Next(128), 128);
                    Script.Stroke(editor, PointerButton.Left, (random.Next(24), random.Next(16)), (random.Next(24), random.Next(16)));
                    break;
            }

            AssertCompositeIsCorrect(editor);
        }
    }
}
