namespace DipsyPaint.Engine.Tools;

/// <summary>What a tool may use while handling one pointer input.</summary>
/// <param name="Target">Pixels of the layer being edited.</param>
/// <param name="PrimaryColor">Colour for the left button.</param>
/// <param name="SecondaryColor">Colour for the right button.</param>
public sealed record ToolContext(IPixelAccess Target, Rgba PrimaryColor, Rgba SecondaryColor);
