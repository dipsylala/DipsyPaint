namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's fill: pressing replaces the contiguous region (4-connected) of exactly the
/// clicked pixel's colour on the active layer with the primary colour (left button) or the
/// secondary colour (right button). See <see cref="FloodRegion"/>.
/// </summary>
public sealed class FillTool : ITool
{
    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var target = context.Target;
        var x = (int)Math.Floor(input.X);
        var y = (int)Math.Floor(input.Y);
        var color = input.Button == PointerButton.Right ? context.SecondaryColor : context.PrimaryColor;
        if (input.Action != PointerAction.Down
            || (uint)x >= (uint)target.Width || (uint)y >= (uint)target.Height
            || target.GetPixel(x, y) == color)
        {
            return IntRect.Empty;
        }

        var row = new Rgba[target.Width];
        Array.Fill(row, color);
        var dirty = IntRect.Empty;
        foreach (var span in FloodRegion.Find(target, x, y))
        {
            target.WriteRegion(span, row);
            dirty = dirty.Union(span);
        }

        return dirty;
    }
}
