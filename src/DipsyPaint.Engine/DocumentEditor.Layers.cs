using System.Runtime.CompilerServices;

namespace DipsyPaint.Engine;

/// <summary>
/// Layer operations, as in reference Paint's layers panel: add, duplicate, delete, merge down,
/// show or hide, and reorder, each one history step; and choosing the active layer, which is not.
/// Each commits pending content first and is ignored during a gesture.
/// </summary>
public sealed partial class DocumentEditor
{
    /// <summary>
    /// Bitmaps the size of the canvas that exist besides the layers: the composite, its cached layers
    /// below and above the active one, and the shell's on-screen bitmap.
    /// </summary>
    private const int BitmapsBesideLayers = 4;

    /// <summary>The compositor's cached layers below and above the active one, which exist only with more than one layer.</summary>
    private int CompositorCaches => Document.Layers.Count > 1 ? 2 : 0;

    /// <summary>
    /// Raised after the layer stack changed through this editor: a layer added, removed, moved, shown
    /// or hidden, or another layer made active. Undo and redo can change it too; they raise
    /// <see cref="History.Changed"/>.
    /// </summary>
    public event EventHandler? LayersChanged;

    /// <summary>Whether <see cref="DeleteLayer"/> may delete a layer: a document keeps at least one.</summary>
    public bool CanDeleteLayer => Document.Layers.Count > 1;

    /// <summary>Whether <paramref name="layer"/> has a layer below it to merge into.</summary>
    public bool CanMergeDown(Layer layer) => IndexOf(layer) > 0;

    /// <summary>
    /// Adds a transparent layer directly above the active layer and makes it active. Refused, changing
    /// nothing, when the document's bitmaps would then exceed <paramref name="budget"/> (default
    /// <see cref="MemoryBudget.Default"/>). Returns false only when refused.
    /// </summary>
    public bool AddLayer(MemoryBudget? budget = null)
    {
        if (!FitsWithOneMoreLayer(budget))
        {
            return false;
        }

        ChangeLayers(() =>
        {
            var layer = new Layer(Document.Width, Document.Height, Rgba.Transparent);
            Document.InsertLayer(IndexOf(Document.ActiveLayer) + 1, layer);
            Document.ActiveLayer = layer;
        });
        return true;
    }

    /// <summary>
    /// Adds a copy of <paramref name="layer"/> (pixels and properties) directly above it and makes the
    /// copy active. Refused like <see cref="AddLayer"/>; returns false only when refused.
    /// </summary>
    public bool DuplicateLayer(Layer layer, MemoryBudget? budget = null)
    {
        var index = IndexOf(layer);
        if (!FitsWithOneMoreLayer(budget))
        {
            return false;
        }

        ChangeLayers(() =>
        {
            var copy = new Layer(layer);
            Document.InsertLayer(index + 1, copy);
            Document.ActiveLayer = copy;
        });
        return true;
    }

    /// <summary>
    /// Deletes <paramref name="layer"/>. If it was active, the layer below it becomes active, or the
    /// one above if it was the bottom layer. Does nothing to the last remaining layer.
    /// </summary>
    public void DeleteLayer(Layer layer)
    {
        var index = IndexOf(layer);
        if (!CanDeleteLayer)
        {
            return;
        }

        ChangeLayers(() =>
        {
            if (Document.ActiveLayer == layer)
            {
                Document.ActiveLayer = Document.Layers[index > 0 ? index - 1 : 1];
            }

            Document.RemoveLayer(layer);
        });
    }

    /// <summary>
    /// Merges <paramref name="layer"/> into the layer below it: its pixels are composited onto that
    /// layer as they are shown (nothing if it is hidden, else at its opacity), it is removed, and the
    /// layer below, which keeps its own properties, becomes active. Does nothing to the bottom layer.
    /// </summary>
    public void MergeDown(Layer layer)
    {
        var index = IndexOf(layer);
        if (index == 0)
        {
            return;
        }

        var lower = Document.Layers[index - 1];
        var recorder = new PixelRecorder(lower.Pixels);
        ChangeLayers(() =>
        {
            MergePixels(layer, recorder);
            Document.RemoveLayer(layer);
            Document.ActiveLayer = lower;
        }, () => recorder.TakeStep());
    }

    /// <summary>Shows or hides <paramref name="layer"/>.</summary>
    public void SetLayerVisible(Layer layer, bool visible)
    {
        IndexOf(layer);
        if (layer.IsVisible != visible)
        {
            ChangeLayers(() => layer.Properties = layer.Properties with { IsVisible = visible });
        }
    }

    /// <summary>
    /// Sets <paramref name="layer"/>'s opacity (0–1). Reference Paint has no layer opacity, so the shell
    /// does not offer this; it is here for layered project files (OpenRaster's <c>opacity</c>).
    /// </summary>
    public void SetLayerOpacity(Layer layer, double opacity)
    {
        IndexOf(layer);
        ArgumentOutOfRangeException.ThrowIfLessThan(opacity, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1);
        if (layer.Opacity != opacity)
        {
            ChangeLayers(() => layer.Properties = layer.Properties with { Opacity = opacity });
        }
    }

    /// <summary>Moves <paramref name="layer"/> to <paramref name="index"/> in <see cref="Document.Layers"/> (0 is the bottom; clamped).</summary>
    public void MoveLayer(Layer layer, int index)
    {
        var from = IndexOf(layer);
        index = Math.Clamp(index, 0, Document.Layers.Count - 1);
        if (index != from)
        {
            ChangeLayers(() => Document.MoveLayer(layer, index));
        }
    }

    /// <summary>Makes <paramref name="layer"/> the one tools draw on. Not a history step.</summary>
    public void SelectLayer(Layer layer)
    {
        IndexOf(layer);
        if (_inGesture || Document.ActiveLayer == layer)
        {
            return;
        }

        CommitPending();
        Document.ActiveLayer = layer;
        Compositor.PrepareCaches();
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    // Applies a change to the layer stack as one history step, then recomposites the whole canvas.
    // pixelStep: takes the step for any pixels the change wrote, after it ran.
    private void ChangeLayers(Action change, Func<HistoryStep?>? pixelStep = null)
    {
        if (_inGesture)
        {
            return;
        }

        CommitPending();
        var before = Document.SaveStack();
        change();
        History.Record(new LayerStackStep(Document, before, pixelStep?.Invoke()));
        Refresh(new IntRect(0, 0, Document.Width, Document.Height), anyLayer: true);
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    // Composites upper onto the layer under it through recorder, writing only the runs of pixels that
    // change, so the history step holds the tiles upper actually covers.
    private void MergePixels(Layer upper, PixelRecorder recorder)
    {
        if (!upper.IsVisible || upper.Opacity <= 0)
        {
            return;
        }

        var width = Document.Width;
        var source = new Rgba[width];
        var target = new Rgba[width];
        var scale = (float)upper.Opacity;
        for (var y = 0; y < Document.Height; y++)
        {
            var row = new IntRect(0, y, width, 1);
            upper.Pixels.ReadRegion(row, source);
            recorder.ReadRegion(row, target);
            int first = -1, last = -1;
            for (var x = 0; x < width; x++)
            {
                var merged = (scale < 1 ? source[x].WithOpacity(scale) : source[x]).Over(target[x]);
                if (merged != target[x])
                {
                    target[x] = merged;
                    first = first < 0 ? x : first;
                    last = x;
                }
            }

            if (first >= 0)
            {
                recorder.WriteRegion(new IntRect(first, y, last - first + 1, 1), target.AsSpan(first, last - first + 1));
            }
        }
    }

    private bool FitsWithOneMoreLayer(MemoryBudget? budget)
    {
        var bitmaps = Document.Layers.Count + 1 + BitmapsBesideLayers;
        return (budget ?? MemoryBudget.Default).Allows(bitmaps * (long)Document.Width * Document.Height * Unsafe.SizeOf<Rgba>());
    }

    // The layer's index in the document; throws for a layer of another document.
    private int IndexOf(Layer layer)
    {
        for (var i = 0; i < Document.Layers.Count; i++)
        {
            if (Document.Layers[i] == layer)
            {
                return i;
            }
        }

        throw new ArgumentException("The layer does not belong to this document.", nameof(layer));
    }
}
