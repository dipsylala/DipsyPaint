using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Magic wand selections at several tolerances, made visible by deleting them (the hole shows as
/// transparency). The 48×32 document is a radial gradient around (14, 16), redder and less blue
/// outward, with a black bar at x = 32 and, beyond it, a block of exactly the centre's colour, which
/// is never selected because it is not connected.
/// </summary>
public class MagicWandGoldenTests
{
    private static readonly Rgba Centre = new(0, 90, 220, 255);

    private static DocumentEditor CreateEditor(int tolerance)
    {
        var settings = new SelectionSettings { Shape = SelectionShape.MagicWand, Tolerance = tolerance };
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { SelectionSettings = settings, HandleTolerance = 0.25 };
        editor.ActiveTool = new SelectionTool(settings);
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var distance = Math.Sqrt((x - 14) * (x - 14) + (y - 16) * (y - 16));
                layer.SetPixel(x, y, x switch
                {
                    < 32 => new Rgba((byte)Math.Min(255, distance * 12), 90, (byte)(220 - Math.Min(220, distance * 4)), 255),
                    32 => Rgba.Black,
                    _ => Centre,
                });
            }
        }

        editor.Compositor.Update(new IntRect(0, 0, layer.Width, layer.Height));
        return editor;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void DeleteAtTolerance(int tolerance)
    {
        var editor = CreateEditor(tolerance);

        Script.Stroke(editor, PointerButton.Left, (14.5, 16.5), (14.5, 16.5));
        editor.DeleteSelection();

        GoldenImage.AssertMatches($"magic-wand-tolerance-{tolerance}", editor.Compositor);
    }
}
