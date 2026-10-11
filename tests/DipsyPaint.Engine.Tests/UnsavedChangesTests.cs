using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class UnsavedChangesTests
{
    // A dot costs its tile's before-pixels; on a 32×32 canvas the tile is the whole canvas.
    private const long OneTile = 32 * 32 * 4;

    private static DocumentEditor CreateEditor(long? capacityBytes = null) =>
        new(Document.CreateBlank(32, 32), capacityBytes is { } cap ? new History(cap) : new History())
        {
            ActiveTool = new PencilTool(),
        };

    private static void Dot(DocumentEditor editor, int x, int y) => Script.Stroke(editor, PointerButton.Left, (x, y));

    [Fact]
    public void ANewEditor_HasNoUnsavedChanges()
    {
        Assert.False(CreateEditor().HasUnsavedChanges);
    }

    [Fact]
    public void AStroke_IsAnUnsavedChange_UntilSaved()
    {
        var editor = CreateEditor();

        Dot(editor, 1, 1);
        Assert.True(editor.HasUnsavedChanges);

        editor.MarkSaved();
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void UndoingBackToTheSavedPoint_ClearsThem_AndRedoingPastItSetsThemAgain()
    {
        var editor = CreateEditor();
        Dot(editor, 1, 1);
        editor.MarkSaved();
        Dot(editor, 2, 2);
        Assert.True(editor.HasUnsavedChanges);

        editor.Undo();
        Assert.False(editor.HasUnsavedChanges);

        editor.Redo();
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void UndoingPastTheSavedPoint_IsAnUnsavedChange_AndRedoingBackToItClearsIt()
    {
        var editor = CreateEditor();
        Dot(editor, 1, 1);
        Dot(editor, 2, 2);
        editor.MarkSaved();

        editor.Undo();
        Assert.True(editor.HasUnsavedChanges);
        editor.Undo();
        Assert.True(editor.HasUnsavedChanges);

        editor.Redo();
        editor.Redo();
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void UndoingEveryStepOfANewDocument_ClearsThem()
    {
        var editor = CreateEditor();
        Dot(editor, 1, 1);
        Dot(editor, 2, 2);

        editor.Undo();
        editor.Undo();

        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void ANewStepAfterUndoingPastTheSavedPoint_LeavesItUnreachable()
    {
        var editor = CreateEditor();
        Dot(editor, 1, 1);
        editor.MarkSaved();
        editor.Undo();

        Dot(editor, 2, 2); // discards the redo step that led to the saved state
        editor.Undo();

        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void ASavedPointDroppedByTheCap_LeavesUnsavedChangesUntilSavedAgain()
    {
        var editor = CreateEditor(capacityBytes: 2 * OneTile);
        Dot(editor, 1, 1);
        editor.MarkSaved();
        Dot(editor, 2, 2);
        Dot(editor, 3, 3); // drops the first step; the saved state is the one after it, still reachable
        Assert.Equal(2 * OneTile, editor.History.SizeBytes);
        Assert.True(editor.HasUnsavedChanges);
        editor.Undo();
        editor.Undo();
        Assert.False(editor.HasUnsavedChanges);

        editor.Redo();
        editor.Redo();
        Dot(editor, 4, 4); // now the saved state is dropped too
        while (editor.History.CanUndo)
        {
            editor.Undo();
            Assert.True(editor.HasUnsavedChanges);
        }

        editor.MarkSaved();
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void AStepLargerThanTheCap_IsStillAnUnsavedChange()
    {
        var editor = CreateEditor(capacityBytes: 0);

        Dot(editor, 1, 1);

        Assert.False(editor.History.CanUndo);
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void EveryStepType_Counts()
    {
        var editor = CreateEditor();

        editor.Transform(ImageTransform.FlipHorizontal);
        Assert.True(editor.HasUnsavedChanges);
        editor.Undo();
        Assert.False(editor.HasUnsavedChanges);

        editor.Resize(16, 16);
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void Clear_KeepsUnsavedChangesUnsaved()
    {
        var clean = CreateEditor();
        clean.History.Clear();
        Assert.False(clean.HasUnsavedChanges);

        var dirty = CreateEditor();
        Dot(dirty, 1, 1);
        dirty.History.Clear();
        Assert.True(dirty.HasUnsavedChanges);
    }

    [Fact]
    public void TheEvent_IsRaisedOnlyWhenTheValueChanges()
    {
        var editor = CreateEditor();
        var seen = new List<bool>();
        editor.UnsavedChangesChanged += (_, _) => seen.Add(editor.HasUnsavedChanges);

        Dot(editor, 1, 1);  // → true
        Dot(editor, 2, 2);  // still true
        editor.MarkSaved(); // → false
        editor.MarkSaved(); // still false
        editor.Undo();      // → true
        editor.Redo();      // → false

        Assert.Equal([true, false, true, false], seen);
    }

    [Fact]
    public void MarkUnsaved_MakesUnsavedChanges_ThatUndoingCannotClear_UntilSaved()
    {
        var editor = CreateEditor();
        var raised = 0;
        editor.UnsavedChangesChanged += (_, _) => raised++;

        editor.History.MarkUnsaved();
        Assert.True(editor.HasUnsavedChanges);
        Assert.Equal(1, raised);

        Dot(editor, 1, 1);
        editor.Undo();
        Assert.True(editor.HasUnsavedChanges);

        editor.MarkSaved();
        Assert.False(editor.HasUnsavedChanges);
    }
}
