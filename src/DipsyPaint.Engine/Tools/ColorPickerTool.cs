namespace DipsyPaint.Engine.Tools;

/// <summary>
/// Reference Paint's colour picker: takes the colour of the composited pixel under the pointer,
/// as an opaque colour, into the primary colour (left button) or secondary colour (right button).
/// It follows the pointer while the button is held. Fully transparent pixels and pixels off the
/// canvas leave the colour unchanged. Changes no pixels.
/// </summary>
public sealed class ColorPickerTool : ITool
{
    public IntRect Handle(ToolContext context, PointerInput input)
    {
        var x = (int)Math.Floor(input.X);
        var y = (int)Math.Floor(input.Y);
        if (context.Composite is not { } composite || (uint)x >= (uint)composite.Width || (uint)y >= (uint)composite.Height)
        {
            return IntRect.Empty;
        }

        var pixel = composite.Pixels[y * composite.Width + x];
        if (pixel.A == 0)
        {
            return IntRect.Empty;
        }

        if (input.Button == PointerButton.Right)
        {
            context.SecondaryColor = pixel.ToOpaque();
        }
        else
        {
            context.PrimaryColor = pixel.ToOpaque();
        }

        return IntRect.Empty;
    }
}
