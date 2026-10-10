namespace DipsyPaint.Engine.Tools;

/// <summary>What a tool may use while handling one pointer input.</summary>
/// <param name="Target">Pixels of the layer being edited.</param>
/// <param name="PrimaryColor">Colour for the left button.</param>
/// <param name="SecondaryColor">Colour for the right button.</param>
public sealed record ToolContext(IPixelAccess Target, Rgba PrimaryColor, Rgba SecondaryColor)
{
    /// <summary>Colour for the left button. A tool may set it (e.g. the colour picker); the editor adopts it.</summary>
    public Rgba PrimaryColor { get; set; } = PrimaryColor;

    /// <summary>Colour for the right button. A tool may set it (e.g. the colour picker); the editor adopts it.</summary>
    public Rgba SecondaryColor { get; set; } = SecondaryColor;

    /// <summary>The composited document (what the user sees), for tools that sample it; null if not provided.</summary>
    public Compositor? Composite { get; init; }

    /// <summary>Pending content shown above the active layer (see <see cref="IPendingTool"/>).</summary>
    public Overlay Overlay { get; init; } = new();

    /// <summary>How far from a handle, in document pixels, the pointer still grabs it; follows the zoom.</summary>
    public double HandleTolerance { get; init; } = DocumentEditor.DefaultHandleTolerance;
}
