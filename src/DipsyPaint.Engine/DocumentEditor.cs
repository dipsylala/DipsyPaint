using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine;

/// <summary>
/// Routes pointer input to the active tool, records each stroke as a history step and keeps the
/// composite up to date. The shell and scripted tests both drive editing through this class.
/// An editor belongs to one document: replacing the document means a new editor and an empty history.
/// </summary>
public sealed class DocumentEditor
{
    // Records the active layer's before-pixels from pointer down to pointer up.
    private PixelRecorder? _recorder;

    public DocumentEditor(Document document) : this(document, new History())
    {
    }

    public DocumentEditor(Document document, History history)
    {
        Document = document;
        Compositor = new Compositor(document);
        History = history;
    }

    public Document Document { get; }
    public Compositor Compositor { get; }
    public History History { get; }

    public ITool? ActiveTool { get; set; }

    /// <summary>Colour for the left button. Fixed default until colour selection exists.</summary>
    public Rgba PrimaryColor { get; set; } = Rgba.Black;

    /// <summary>Colour for the right button. Fixed default until colour selection exists.</summary>
    public Rgba SecondaryColor { get; set; } = Rgba.White;

    /// <summary>Raised with the document-space rectangle whose composited pixels changed.</summary>
    public event EventHandler<IntRect>? CompositeChanged;

    public void Apply(PointerInput input)
    {
        if (ActiveTool is null)
        {
            return;
        }

        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        var context = new ToolContext(_recorder, PrimaryColor, SecondaryColor);
        var dirty = ActiveTool.Handle(context, input);
        if (input.Action == PointerAction.Up)
        {
            if (_recorder.TakeStep() is { } step)
            {
                History.Record(step);
            }

            _recorder = null;
        }

        Refresh(dirty);
    }

    /// <summary>Undoes the latest history step. Ignored while a stroke is in progress.</summary>
    public void Undo()
    {
        if (_recorder is null)
        {
            Refresh(History.Undo());
        }
    }

    /// <summary>Redoes the latest undone history step. Ignored while a stroke is in progress.</summary>
    public void Redo()
    {
        if (_recorder is null)
        {
            Refresh(History.Redo());
        }
    }

    private void Refresh(IntRect dirty)
    {
        if (dirty.IsEmpty)
        {
            return;
        }

        Compositor.Update(dirty);
        CompositeChanged?.Invoke(this, dirty);
    }
}
