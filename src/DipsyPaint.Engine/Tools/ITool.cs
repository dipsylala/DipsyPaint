namespace DipsyPaint.Engine.Tools;

/// <summary>An editing tool driven by pointer input as data.</summary>
public interface ITool
{
    /// <summary>Applies one pointer input and returns the rectangle of pixels it changed (empty if none).</summary>
    IntRect Handle(ToolContext context, PointerInput input);
}
