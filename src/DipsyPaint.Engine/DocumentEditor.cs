using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine;

/// <summary>
/// Routes pointer input to the active tool, records each stroke as a history step and keeps the
/// composite up to date. The shell and scripted tests both drive editing through this class.
/// An editor belongs to one document: replacing the document means a new editor and an empty history.
/// Pending content of an <see cref="IPendingTool"/> is shown in <see cref="Overlay"/>. Everything
/// written to the layer from the gesture that creates it until it is committed (e.g. the hole left by
/// lifting a selection) becomes one history step; cancelling reverts those writes.
/// </summary>
public sealed class DocumentEditor
{
    /// <summary>Handle grab distance at 100% zoom, in document pixels.</summary>
    public const double DefaultHandleTolerance = 4;

    // Records the active layer's before-pixels from pointer down to pointer up, or until pending
    // content is committed or cancelled.
    private PixelRecorder? _recorder;
    private bool _inGesture;
    private ITool? _activeTool;
    private Rgba _primaryColor = Rgba.Black;
    private Rgba _secondaryColor = Rgba.White;

    public DocumentEditor(Document document) : this(document, new History())
    {
    }

    public DocumentEditor(Document document, History history)
    {
        Document = document;
        Overlay = new Overlay();
        Compositor = new Compositor(document, Overlay);
        History = history;
    }

    public Document Document { get; }
    public Compositor Compositor { get; }
    public History History { get; }

    /// <summary>Pending content, composited above the active layer but not yet part of it.</summary>
    public Overlay Overlay { get; }

    /// <summary>The tool pointer input goes to. Changing it commits the old tool's pending content.</summary>
    public ITool? ActiveTool
    {
        get => _activeTool;
        set
        {
            CommitPending();
            _activeTool = value;
        }
    }

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

    /// <summary>
    /// How far from a handle, in document pixels, the pointer still grabs it. The shell sets it from
    /// the zoom so handles keep a constant on-screen size.
    /// </summary>
    public double HandleTolerance { get; set; } = DefaultHandleTolerance;

    /// <summary>The frame and handles to draw around pending content, or null.</summary>
    public Adornment? Adornment => (ActiveTool as IPendingTool)?.Adornment;

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

        // A press outside pending content commits it, as in reference Paint; the tool then sees a fresh press.
        if (input.Action == PointerAction.Down && !_inGesture
            && Adornment?.HitTest(input.X, input.Y, HandleTolerance) == FrameHandle.None)
        {
            CommitPending();
        }

        _inGesture = input.Action != PointerAction.Up;
        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        var context = Context(_recorder);
        var dirty = ActiveTool.Handle(context, input);
        PrimaryColor = context.PrimaryColor;
        SecondaryColor = context.SecondaryColor;
        if (input.Action == PointerAction.Up && Adornment is null)
        {
            RecordStep();
        }

        Refresh(dirty);
        if (input.Action == PointerAction.Up)
        {
            StrokeEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Commits the active tool's pending content to the active layer as one history step (Enter).
    /// During a gesture the commit joins that gesture's step instead.
    /// </summary>
    public void CommitPending()
    {
        if (ActiveTool is not IPendingTool tool)
        {
            return;
        }

        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        var dirty = tool.Commit(Context(_recorder));
        if (!_inGesture)
        {
            RecordStep();
        }

        Refresh(dirty);
    }

    /// <summary>
    /// Discards the active tool's pending content (Esc) and reverts what was written to the layer
    /// while it was pending.
    /// </summary>
    public void CancelPending()
    {
        if (ActiveTool is not IPendingTool tool)
        {
            return;
        }

        var dirty = tool.Cancel(Context(Document.ActiveLayer.Pixels));
        if (!_inGesture)
        {
            if (_recorder?.TakeStep() is { } written)
            {
                dirty = dirty.Union(written.Undo());
            }

            _recorder = null;
        }

        Refresh(dirty);
    }

    /// <summary>Re-renders pending content after a colour or tool option changed.</summary>
    public void RefreshPending()
    {
        if (ActiveTool is IPendingTool tool)
        {
            Refresh(tool.Refresh(Context(Document.ActiveLayer.Pixels)));
        }
    }

    /// <summary>
    /// Commits pending content, then undoes the latest history step, so undo first removes the
    /// pending content (and redo restores it). Ignored while a stroke is in progress.
    /// </summary>
    public void Undo()
    {
        if (!_inGesture)
        {
            CommitPending();
            Refresh(History.Undo());
        }
    }

    /// <summary>Redoes the latest undone history step. Ignored while a stroke is in progress.</summary>
    public void Redo()
    {
        if (!_inGesture)
        {
            Refresh(History.Redo());
        }
    }

    private void RecordStep()
    {
        if (_recorder?.TakeStep() is { } step)
        {
            History.Record(step);
        }

        _recorder = null;
    }

    private ToolContext Context(IPixelAccess target) =>
        new(target, PrimaryColor, SecondaryColor) { Composite = Compositor, Overlay = Overlay, HandleTolerance = HandleTolerance };

    // Pending content follows the colours, as in reference Paint.
    private void SetColor(ref Rgba field, Rgba value)
    {
        if (field != value)
        {
            field = value;
            ColorsChanged?.Invoke(this, EventArgs.Empty);
            RefreshPending();
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
