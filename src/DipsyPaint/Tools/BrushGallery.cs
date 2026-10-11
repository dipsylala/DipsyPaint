using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Tools;

/// <summary>One brush the Brush tool offers, as in reference Paint's Brushes menu.</summary>
/// <param name="Id">Stable, culture-independent identifier.</param>
/// <param name="DisplayName">Localised name, from a <c>.resx</c>; also the menu entry's automation name.</param>
/// <param name="IconData">Path data for a 24×24 icon.</param>
/// <param name="CreateTool">Creates a fresh engine tool for one use of the brush, with the Brush tool's settings.</param>
public sealed record BrushDescriptor(string Id, string DisplayName, string IconData, Func<StrokeSettings, ITool> CreateTool);

/// <summary>
/// The brushes the Brush tool offers and which one is selected. App-wide, like the other tool
/// settings: the choice survives reselecting the tool.
/// </summary>
public sealed class BrushGallery
{
    public BrushGallery(IReadOnlyList<BrushDescriptor> brushes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(brushes.Count);
        Brushes = brushes;
        Selected = brushes[0];
    }

    /// <summary>The brushes in menu order; the first is selected at start.</summary>
    public IReadOnlyList<BrushDescriptor> Brushes { get; }

    public BrushDescriptor Selected
    {
        get;
        set => field = Brushes.Contains(value)
            ? value
            : throw new ArgumentException("The brush is not in this gallery.", nameof(value));
    }
}
