using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>
/// The pending-content contract as a selection would use it: lifting leaves a hole in the layer,
/// the lifted pixels float in the overlay, and committing puts them down; all one history step.
/// </summary>
public class PendingContentTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>Lifts the pixel under the first press (leaving white) and drags it around until committed.</summary>
    private sealed class LiftTool : IPendingTool
    {
        private bool _dragging;
        private (int X, int Y) _start;
        private IntRect _startBounds;

        public Adornment? Adornment { get; private set; }

        public IntRect Handle(ToolContext context, PointerInput input)
        {
            var (x, y) = ((int)Math.Floor(input.X), (int)Math.Floor(input.Y));
            if (input.Action == PointerAction.Down)
            {
                var dirty = IntRect.Empty;
                if (context.Overlay.IsEmpty)
                {
                    dirty = context.Overlay.Show(new IntRect(x, y, 1, 1), [context.Target.GetPixel(x, y)]);
                    context.Target.SetPixel(x, y, Rgba.White);
                }

                (_dragging, _start, _startBounds, Adornment) = (true, (x, y), context.Overlay.Bounds, null);
                return dirty;
            }

            if (!_dragging)
            {
                return IntRect.Empty;
            }

            var bounds = _startBounds with { X = _startBounds.X + x - _start.X, Y = _startBounds.Y + y - _start.Y };
            var moved = context.Overlay.Show(bounds, [context.Overlay.GetPixel(context.Overlay.Bounds.X, context.Overlay.Bounds.Y)]);
            if (input.Action == PointerAction.Up)
            {
                (_dragging, Adornment) = (false, new Adornment(bounds));
            }

            return moved;
        }

        public IntRect Commit(ToolContext context)
        {
            var dirty = context.Overlay.DrawOnto(context.Target);
            return Cancel(context).Union(dirty);
        }

        public IntRect Cancel(ToolContext context)
        {
            Adornment = null;
            return context.Overlay.Clear();
        }

        public IntRect Refresh(ToolContext context) => IntRect.Empty;
    }

    private static DocumentEditor CreateEditor()
    {
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { ActiveTool = new LiftTool() };
        editor.Document.ActiveLayer.Pixels.SetPixel(2, 2, Red);
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));
        return editor;
    }

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    [Fact]
    public void LiftMoveAndMoveAgain_ThenCommit_IsOneUndoableStep()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (10.5, 2.5));   // lift and move
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 2));                // the hole is already on the layer
        Assert.False(editor.History.CanUndo);                                   // but not yet in history

        Script.Stroke(editor, PointerButton.Left, (10.5, 2.5), (20.5, 2.5));  // move again (press on the content)
        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(20, 2));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(2, 2));

        editor.Undo();

        Assert.Equal(Red, Layer(editor).GetPixel(2, 2));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(20, 2));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void PressOutside_CommitsBeforeTheToolSeesThePress()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (10.5, 2.5));

        editor.Apply(new PointerInput(PointerAction.Down, 25.5, 25.5));

        Assert.Equal(Red, Layer(editor).GetPixel(10, 2));
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void Cancel_RevertsWhatWasWrittenWhilePending()
    {
        var editor = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5), (10.5, 2.5));

        editor.CancelPending();

        Assert.Equal(Red, Layer(editor).GetPixel(2, 2));
        Assert.Equal(Red, editor.Compositor.Pixels[2 * 32 + 2]);
        Assert.Equal(Rgba.White, editor.Compositor.Pixels[2 * 32 + 10]);
        Assert.False(editor.History.CanUndo);
    }
}
