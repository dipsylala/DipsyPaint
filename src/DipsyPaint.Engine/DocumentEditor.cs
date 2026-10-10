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
    private Rgba _primaryColor = Rgba.Black;
    private Rgba _secondaryColor = Rgba.White;

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

    /// <summary>Colour for the left button ("Colour 1"); black by default, as in reference Paint.</summary>
    public Rgba PrimaryColor
    {
        get => _primaryColor;
        set => SetColor(ref _primaryColor, value);
    }

    /// <summary>Colour for the right button ("Colour 2"); white by default, as in reference Paint.</summary>
    public Rgba SecondaryColor
    {
        get => _secondaryColor;
        set => SetColor(ref _secondaryColor, value);
    }

    /// <summary>Raised with the document-space rectangle whose composited pixels changed.</summary>
    public event EventHandler<IntRect>? CompositeChanged;

    /// <summary>Raised when <see cref="PrimaryColor"/> or <see cref="SecondaryColor"/> changes, e.g. by a tool.</summary>
    public event EventHandler? ColorsChanged;

    /// <summary>Raised after the active tool has handled a pointer up, i.e. at the end of each stroke or click.</summary>
    public event EventHandler? StrokeEnded;

    public void Apply(PointerInput input)
    {
        if (ActiveTool is null)
        {
            return;
        }

        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        var context = new ToolContext(_recorder, PrimaryColor, SecondaryColor) { Composite = Compositor };
        var dirty = ActiveTool.Handle(context, input);
        PrimaryColor = context.PrimaryColor;
        SecondaryColor = context.SecondaryColor;
        if (input.Action == PointerAction.Up)
        {
            if (_recorder.TakeStep() is { } step)
            {
                History.Record(step);
            }

            _recorder = null;
        }

        Refresh(dirty);
        if (input.Action == PointerAction.Up)
        {
            StrokeEnded?.Invoke(this, EventArgs.Empty);
        }
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

    private void SetColor(ref Rgba field, Rgba value)
    {
        if (field != value)
        {
            field = value;
            ColorsChanged?.Invoke(this, EventArgs.Empty);
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
