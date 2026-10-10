using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class HistoryTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static DocumentEditor CreateEditor(int width = 32, int height = 32, long? capacityBytes = null) =>
        new(Document.CreateBlank(width, height), capacityBytes is { } cap ? new History(cap) : new History())
        {
            ActiveTool = new PencilTool(),
        };

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    [Fact]
    public void Undo_RestoresPixelsExactly()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (1, 1), (30, 20));
        var before = Snapshot(editor);

        editor.SecondaryColor = Red.WithOpacity(0.5f); // blends with what is underneath
        Script.Stroke(editor, PointerButton.Right, (0, 20), (31, 0), (5, 31));
        Assert.NotEqual(before, Snapshot(editor));

        editor.Undo();

        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(before, editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void Redo_ReappliesTheStepExactly()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (1, 1), (30, 20));
        Script.Stroke(editor, PointerButton.Left, (0, 20), (31, 0));
        var after = Snapshot(editor);

        editor.Undo();
        editor.Undo();
        editor.Redo();
        editor.Redo();

        Assert.Equal(after, Snapshot(editor));
        Assert.Equal(after, editor.Compositor.Pixels.ToArray());
        Assert.False(editor.History.CanRedo);
    }

    [Fact]
    public void UndoAndRedo_ReportTheChangedRectangle()
    {
        var editor = CreateEditor();
        var changes = new List<IntRect>();
        Script.Stroke(editor, PointerButton.Left, (3, 4));
        editor.CompositeChanged += (_, dirty) => changes.Add(dirty);

        editor.Undo();
        editor.Redo();

        Assert.Equal(2, changes.Count);
        Assert.All(changes, dirty => Assert.Equal(dirty, dirty.Union(new IntRect(3, 4, 1, 1))));
    }

    [Fact]
    public void NewStep_ClearsRedo()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (1, 1));
        Script.Stroke(editor, PointerButton.Left, (2, 2));
        editor.Undo();
        Assert.True(editor.History.CanRedo);

        Script.Stroke(editor, PointerButton.Left, (3, 3));

        Assert.False(editor.History.CanRedo);
        editor.Redo();
        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(2, 2));
        editor.Undo();
        editor.Undo();
        Assert.False(editor.History.CanUndo);
        Assert.Equal(Snapshot(CreateEditor()), Snapshot(editor));
    }

    [Fact]
    public void UndoAndRedo_WithNothingToDo_ChangeNothing()
    {
        var editor = CreateEditor();
        var raised = false;
        editor.CompositeChanged += (_, _) => raised = true;

        editor.Undo();
        editor.Redo();

        Assert.False(raised);
    }

    [Fact]
    public void UndoDuringAStroke_IsIgnored()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (1, 1));
        editor.Apply(new PointerInput(PointerAction.Down, 5, 5));

        editor.Undo();
        editor.Apply(new PointerInput(PointerAction.Up, 5, 5));

        Assert.Equal(Rgba.Black, editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
        editor.Undo();
        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(5, 5));
        Assert.Equal(Rgba.Black, editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
    }

    [Fact]
    public void StepMemory_IsProportionalToChangedArea()
    {
        var editor = CreateEditor(4000, 4000);
        var layerBytes = 4000L * 4000 * 4;

        Script.Stroke(editor, PointerButton.Left, (10, 10));
        var dotBytes = editor.History.SizeBytes;
        Script.Stroke(editor, PointerButton.Left, (0, 0), (3999, 3999));
        var diagonalBytes = editor.History.SizeBytes - dotBytes;

        Assert.InRange(dotBytes, 4, 64 * 64 * 4);
        // A full-canvas diagonal touches a thin band of tiles, not its whole bounding box.
        Assert.True(diagonalBytes < layerBytes / 20, $"{diagonalBytes} bytes for a diagonal on a {layerBytes}-byte layer");
    }

    [Fact]
    public void Cap_DropsOldestStepsFirst()
    {
        // Each dot lies in its own 64×64 tile of a 64×256 canvas, so each step holds 16 KiB.
        const long stepBytes = 64 * 64 * 4;
        var editor = CreateEditor(64, 256, capacityBytes: 2 * stepBytes);

        Script.Stroke(editor, PointerButton.Left, (1, 1));
        Script.Stroke(editor, PointerButton.Left, (1, 65));
        Script.Stroke(editor, PointerButton.Left, (1, 129));

        Assert.Equal(2 * stepBytes, editor.History.SizeBytes);
        editor.Undo();
        editor.Undo();
        Assert.False(editor.History.CanUndo);
        var pixels = editor.Document.ActiveLayer.Pixels;
        Assert.Equal(Rgba.Black, pixels.GetPixel(1, 1)); // oldest step was dropped, so it stays
        Assert.Equal(Rgba.White, pixels.GetPixel(1, 65));
        Assert.Equal(Rgba.White, pixels.GetPixel(1, 129));
    }

    [Fact]
    public void Cap_IsNeverExceeded_EvenByASingleLargeStep()
    {
        var editor = CreateEditor(64, 64, capacityBytes: 100);

        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(0, editor.History.SizeBytes);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DefaultCap_IsAtMostOneGigabyteAndAQuarterOfMemory()
    {
        var quarter = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 4;

        Assert.Equal(Math.Min(1L << 30, quarter), History.DefaultCapacityBytes);
        Assert.True(History.DefaultCapacityBytes > 0);
        Assert.Equal(History.DefaultCapacityBytes, new DocumentEditor(Document.CreateBlank()).History.CapacityBytes);
    }

    [Fact]
    public void Clear_DropsAllSteps()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (1, 1));
        Script.Stroke(editor, PointerButton.Left, (2, 2));
        editor.Undo();

        editor.History.Clear();

        Assert.False(editor.History.CanUndo);
        Assert.False(editor.History.CanRedo);
        Assert.Equal(0, editor.History.SizeBytes);
    }

    [Fact]
    public void Changed_IsRaisedWhenUndoOrRedoAvailabilityChanges()
    {
        var editor = CreateEditor();
        var raised = 0;
        editor.History.Changed += (_, _) => raised++;

        Script.Stroke(editor, PointerButton.Left, (1, 1));
        editor.Undo();
        editor.Redo();
        editor.History.Clear();

        Assert.Equal(4, raised);
    }
}
