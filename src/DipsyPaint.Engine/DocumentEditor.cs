using System.Runtime.CompilerServices;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine;

/// <summary>
/// Routes pointer input to the active tool, records each stroke as a history step and keeps the
/// composite up to date. The shell and scripted tests both drive editing through this class.
/// An editor belongs to one document: replacing the document means a new editor and an empty history.
/// Pending content of an <see cref="IPendingTool"/> is shown in <see cref="Overlay"/>. Everything
/// written to the layer from the gesture that creates it until it is committed (e.g. the hole left by
/// lifting a selection) becomes one history step; cancelling reverts those writes.
/// The image operations (<see cref="CropToSelection"/>, <see cref="Resize"/>, <see cref="Transform"/>)
/// apply to the selection when there is one, else to every layer, and may change the canvas size
/// (<see cref="SizeChanged"/>).
/// </summary>
public sealed partial class DocumentEditor
{
    /// <summary>Handle grab distance at 100% zoom, in document pixels.</summary>
    public const double DefaultHandleTolerance = 4;

    // Records the active layer's before-pixels from pointer down to pointer up, or until pending
    // content is committed or cancelled.
    private PixelRecorder? _recorder;
    private bool _inGesture;
    private ITool? _activeTool;
    private Selection? _selection;
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

    /// <summary>
    /// Whether the document has <i>unsaved changes</i>: history steps made, undone or redone since it
    /// was last saved, opened or created. Tracked by <see cref="History"/>, so every step type counts.
    /// </summary>
    public bool HasUnsavedChanges => History.HasUnsavedChanges;

    /// <summary>Raised when <see cref="HasUnsavedChanges"/> changes.</summary>
    public event EventHandler? UnsavedChangesChanged
    {
        add => History.UnsavedChangesChanged += value;
        remove => History.UnsavedChangesChanged -= value;
    }

    /// <summary>Records that the document, as it is now, has been saved.</summary>
    public void MarkSaved() => History.MarkSaved();

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
            ActiveToolChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>Whether the active tool has pending content, with or without an <see cref="Adornment"/> (Enter commits it, Esc cancels it).</summary>
    public bool HasPending => (ActiveTool as IPendingTool)?.HasPending == true;

    /// <summary>The selection of the active <see cref="SelectionTool"/>, or null when there is none.</summary>
    public Selection? Selection => (ActiveTool as SelectionTool)?.Selection;

    /// <summary>
    /// The options of the selection tools the editor creates itself (<see cref="Paste"/>,
    /// <see cref="Select"/>); the shell shares one instance with its own selection tools.
    /// </summary>
    public SelectionSettings SelectionSettings { get; set; } = new();

    /// <summary>Raised with the document-space rectangle, within the canvas, whose composited pixels changed.</summary>
    public event EventHandler<IntRect>? CompositeChanged;

    /// <summary>
    /// Raised when the canvas size changed (an image operation, or undoing or redoing one), after
    /// <see cref="Compositor"/> has been resized and before the matching <see cref="CompositeChanged"/>.
    /// </summary>
    public event EventHandler? SizeChanged;

    /// <summary>Raised when <see cref="PrimaryColor"/> or <see cref="SecondaryColor"/> changes, e.g. by a tool.</summary>
    public event EventHandler? ColorsChanged;

    /// <summary>Raised after the active tool has handled a pointer up, i.e. at the end of each stroke or click.</summary>
    public event EventHandler? StrokeEnded;

    /// <summary>Raised when <see cref="ActiveTool"/> is set, including by <see cref="Paste"/>.</summary>
    public event EventHandler? ActiveToolChanged;

    /// <summary>Raised when <see cref="Selection"/> changes: made, moved, stretched or removed.</summary>
    public event EventHandler? SelectionChanged;

    public void Apply(PointerInput input)
    {
        if (ActiveTool is null)
        {
            return;
        }

        // A press outside pending content commits it, as in reference Paint; the tool then sees a fresh press.
        var committed = false;
        if (input.Action == PointerAction.Down && !_inGesture
            && Adornment?.HitTest(input.X, input.Y, HandleTolerance) == FrameHandle.None)
        {
            CommitPending();
            committed = true;
        }

        _inGesture = input.Action != PointerAction.Up;
        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        var context = Context(_recorder) with { CommittedByPress = committed };
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
    /// Deletes the selected pixels (Delete), leaving the layer transparent there, and removes the
    /// selection; with any move or stretch before it, one history step. Ignored during a gesture.
    /// </summary>
    public void DeleteSelection()
    {
        if (_inGesture || ActiveTool is not SelectionTool tool)
        {
            return;
        }

        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        Refresh(tool.Delete(Context(_recorder)));
        RecordStep();
    }

    /// <summary>
    /// A copy of the selected pixels (Copy), or null when nothing is selected: the lifted pixels
    /// as shown, else the active layer's within the selection, transparent outside its shape (see
    /// <see cref="SelectionTool.Copy"/>). The selection is left as it is. Cut is a copy followed by
    /// <see cref="DeleteSelection"/>.
    /// </summary>
    public PixelImage? CopySelection() => (ActiveTool as SelectionTool)?.Copy(Context(Document.ActiveLayer.Pixels));

    /// <summary>Selects the whole canvas (Select all), as <see cref="Select"/>.</summary>
    public void SelectAll() => Select(new IntRect(0, 0, Document.Width, Document.Height));

    /// <summary>
    /// Selects every pixel of the canvas outside the selection (Invert selection), or the whole canvas
    /// without one, as <see cref="Select"/>; a moved selection is committed first, and its shape where it
    /// now is is inverted. Inverting a selection of the whole canvas leaves nothing selected.
    /// </summary>
    public void InvertSelection()
    {
        if (_inGesture)
        {
            return;
        }

        var canvas = new IntRect(0, 0, Document.Width, Document.Height);
        if (SelectionMasks.Invert(Selection, canvas) is { } inverse)
        {
            Select(inverse.Bounds, inverse.Mask);
        }
        else
        {
            CommitPending();
        }
    }

    /// <summary>
    /// Selects <paramref name="bounds"/> (within the canvas), or the pixels set in <paramref name="mask"/>
    /// within it (see <see cref="Selection.Mask"/>; build one with <see cref="SelectionMasks"/>), switching
    /// to the selection tool as reference Paint does; pending content (including a current selection) is
    /// committed first. Selecting writes nothing. The editor keeps the mask. Ignored during a gesture.
    /// </summary>
    public void Select(IntRect bounds, bool[]? mask = null)
    {
        if (_inGesture)
        {
            return;
        }

        if (ActiveTool is SelectionTool)
        {
            CommitPending();
        }
        else
        {
            ActiveTool = new SelectionTool(SelectionSettings);
        }

        var tool = (SelectionTool)ActiveTool!;
        Refresh(tool.Select(Context(Document.ActiveLayer.Pixels), bounds, mask));
    }

    /// <summary>
    /// Pastes <paramref name="image"/> as a lifted selection with its top-left at (<paramref name="x"/>,
    /// <paramref name="y"/>), switching to the selection tool as reference Paint does; pending content
    /// (including a current selection) is committed first. If the image reaches past the canvas, the
    /// canvas is first enlarged to fit it, as one history step, up to <see cref="Document.MaxSide"/> and
    /// unless that would exceed <paramref name="budget"/> (default <see cref="MemoryBudget.Default"/>);
    /// the new area is white on the bottom layer and transparent above, as in a new document. Nothing
    /// is written to the layer until the pasted selection is committed, which is one history step.
    /// The editor keeps <see cref="PixelImage.Pixels"/>. Ignored during a gesture.
    /// </summary>
    public void Paste(PixelImage image, int x, int y, MemoryBudget? budget = null)
    {
        if (_inGesture)
        {
            return;
        }

        if (ActiveTool is SelectionTool)
        {
            CommitPending();
        }
        else
        {
            ActiveTool = new SelectionTool(SelectionSettings);
        }

        var (oldWidth, oldHeight) = (Document.Width, Document.Height);
        var width = Math.Clamp(x + image.Width, oldWidth, Document.MaxSide);
        var height = Math.Clamp(y + image.Height, oldHeight, Document.MaxSide);
        // As for Resize: the new layers, the composite and the shell's bitmap.
        var bytes = (Document.Layers.Count + 2) * (long)width * height * Unsafe.SizeOf<Rgba>();
        if ((width, height) != (oldWidth, oldHeight) && (budget ?? MemoryBudget.Default).Allows(bytes))
        {
            ReplaceCanvas(width, height, (layer, pixels) =>
                PixelOps.Extend(pixels, oldWidth, oldHeight, width, height, layer == 0 ? Rgba.White : Rgba.Transparent));
        }

        var tool = (SelectionTool)ActiveTool!;
        Refresh(tool.Paste(Context(Document.ActiveLayer.Pixels), image, x, y));
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
            Refresh(History.Undo(), anyLayer: true);
        }
    }

    /// <summary>
    /// Commits pending content, then redoes the latest undone history step. Pending content that wrote
    /// to the layer (e.g. a lifted selection) is a new step, so nothing is left to redo; committing it
    /// first keeps a redone canvas resize from landing under it. Ignored while a stroke is in progress.
    /// </summary>
    public void Redo()
    {
        if (!_inGesture)
        {
            CommitPending();
            Refresh(History.Redo(), anyLayer: true);
        }
    }

    /// <summary>
    /// Rotates or flips the selection if there is one (it stays pending, as after a move), else every
    /// layer and the canvas, as one history step. Ignored during a gesture.
    /// </summary>
    public void Transform(ImageTransform transform)
    {
        if (_inGesture)
        {
            return;
        }

        if (ActiveTool is SelectionTool { Selection: { } selection } tool)
        {
            var (width, height) = transform.SwapsSides()
                ? (selection.Bounds.Height, selection.Bounds.Width)
                : (selection.Bounds.Width, selection.Bounds.Height);
            ReplaceSelection(tool, width, height, (pixels, w, h) => PixelOps.Transform(pixels, w, h, transform));
            return;
        }

        CommitPending();
        CanvasTransformStep.Apply(Document, transform);
        History.Record(new CanvasTransformStep(Document, transform));
        Refresh(new IntRect(0, 0, Document.Width, Document.Height), anyLayer: true);
    }

    /// <summary>
    /// Resamples the selection to <paramref name="width"/>×<paramref name="height"/> if there is one (it
    /// stays pending), else every layer and the canvas, as one history step; the filter is a triangle
    /// (bilinear) filter, area-weighted when shrinking. Refused beyond <see cref="Document.MaxSide"/>
    /// per side, or when the new pixels would exceed <paramref name="budget"/> (default
    /// <see cref="MemoryBudget.Default"/>). Does nothing during a gesture or when the size is unchanged.
    /// </summary>
    public ResizeResult Resize(int width, int height, MemoryBudget? budget = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (width > Document.MaxSide || height > Document.MaxSide)
        {
            return ResizeResult.TooLarge;
        }

        var tool = ActiveTool as SelectionTool;
        var selection = tool?.Selection;
        // The new layers (or selection), the resampler's intermediate, the composite and the shell's bitmap.
        var bitmaps = selection is null ? Document.Layers.Count + 3 : 2;
        if (!(budget ?? MemoryBudget.Default).Allows(bitmaps * width * (long)height * Unsafe.SizeOf<Rgba>()))
        {
            return ResizeResult.OverMemoryBudget;
        }

        if (_inGesture)
        {
            return ResizeResult.Resized;
        }

        if (selection is { Bounds: var bounds })
        {
            if ((bounds.Width, bounds.Height) != (width, height))
            {
                ReplaceSelection(tool!, width, height, (pixels, w, h) => PixelOps.Resample(pixels, w, h, width, height));
            }

            return ResizeResult.Resized;
        }

        CommitPending();
        var (oldWidth, oldHeight) = (Document.Width, Document.Height);
        if ((oldWidth, oldHeight) != (width, height))
        {
            ReplaceCanvas(width, height, (_, pixels) => PixelOps.Resample(pixels, oldWidth, oldHeight, width, height));
        }

        return ResizeResult.Resized;
    }

    /// <summary>
    /// Commits the selection, then crops every layer and the canvas to it (the part on the canvas) as
    /// one history step. Does nothing without a selection or during a gesture.
    /// </summary>
    public void CropToSelection()
    {
        if (_inGesture || Selection is not { } selection)
        {
            return;
        }

        var canvas = new IntRect(0, 0, Document.Width, Document.Height);
        var region = selection.Bounds.Intersect(canvas);
        CommitPending();
        if (!region.IsEmpty && region != canvas)
        {
            ReplaceCanvas(region.Width, region.Height, (_, pixels) => PixelOps.Crop(pixels, canvas.Width, region));
        }
    }

    private void ReplaceSelection(SelectionTool tool, int width, int height, Func<Rgba[], int, int, Rgba[]> map)
    {
        _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
        Refresh(tool.Replace(Context(_recorder), width, height, map));
    }

    // map: from a layer's index and pixels to its new pixels.
    private void ReplaceCanvas(int width, int height, Func<int, Rgba[], Rgba[]> map)
    {
        var beforeSize = (Document.Width, Document.Height);
        var before = Document.ReplacePixels(width, height, map);
        History.Record(new CanvasPixelsStep(Document, before, beforeSize));
        Refresh(new IntRect(0, 0, width, height), anyLayer: true);
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

    // Every change to the selection ends here, so this is where SelectionChanged is raised.
    // anyLayer: pixels of layers other than the active one may have changed (undo, image operations,
    // layer operations); otherwise only the active layer or the overlay did, and the compositor's
    // cached layers above and below it are reused.
    private void Refresh(IntRect dirty, bool anyLayer = false)
    {
        if (Selection != _selection)
        {
            _selection = Selection;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (Compositor.FitDocument())
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
            CompositeChanged?.Invoke(this, new IntRect(0, 0, Compositor.Width, Compositor.Height));
            return;
        }

        // Pending content may reach past the canvas (e.g. a pasted image larger than it).
        dirty = dirty.Intersect(new IntRect(0, 0, Document.Width, Document.Height));
        if (dirty.IsEmpty)
        {
            return;
        }

        if (anyLayer)
        {
            Compositor.Update(dirty);
        }
        else
        {
            Compositor.UpdateActive(dirty);
        }

        CompositeChanged?.Invoke(this, dirty);
    }
}
