using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class CompositorTests
{
    [Fact]
    public void Composites_LayersSourceOverBottomFirst()
    {
        var document = Document.CreateBlank(2, 1);
        document.AddLayer().Pixels.SetPixel(0, 0, new Rgba(0, 0, 128, 128)); // half-transparent blue

        var compositor = new Compositor(document);

        Assert.Equal(new Rgba(127, 127, 255, 255), compositor.Pixels[0]);
        Assert.Equal(Rgba.White, compositor.Pixels[1]);
    }

    [Fact]
    public void Composites_AsOverDoesPixelByPixel_WhateverThePixels()
    {
        // Rows are blended several pixels at a time (issue #115); the result must be Over's, for runs of
        // transparent, opaque and part-transparent pixels, malformed ones (a channel above alpha) and odd row ends.
        var random = new Random(115);
        Rgba Any() => random.Next(4) switch
        {
            0 => Rgba.Transparent,
            1 => new Rgba((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255),
            2 => new Rgba((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)),
            _ => new Rgba((byte)random.Next(64), (byte)random.Next(64), (byte)random.Next(64), (byte)random.Next(64)),
        };
        const int Width = 37, Height = 5;
        var document = Document.CreateBlank(Width, Height);
        var (bottom, middle, top) = (document.ActiveLayer, document.AddLayer(), document.AddLayer());
        document.ActiveLayer = middle;
        var layers = new[] { bottom, middle, top };
        var expected = new Rgba[Width * Height];
        var overlay = new Rgba[Width * Height];
        for (var i = 0; i < expected.Length; i++)
        {
            var (x, y) = (i % Width, i / Width);
            // Runs of the same kind of pixel, as well as single ones.
            var pixels = layers.Select(_ => random.Next(3) == 0 ? Rgba.Transparent : Any()).ToArray();
            for (var layer = 0; layer < layers.Length; layer++)
            {
                layers[layer].Pixels.SetPixel(x, y, pixels[layer]);
            }

            overlay[i] = x < 8 ? Rgba.Transparent : x < 16 ? new Rgba(10, 20, 30, 255) : Any();
            expected[i] = pixels[2].Over(overlay[i].Over(pixels[1].Over(pixels[0])));
        }

        var shown = new Overlay();
        var compositor = new Compositor(document, shown);
        compositor.Update(shown.Show(new IntRect(0, 0, Width, Height), overlay));

        Assert.Equal(expected, compositor.Pixels.ToArray());
    }

    [Fact]
    public void Update_RecompositesOnlyTheDirtyRectangle()
    {
        var document = Document.CreateBlank(4, 1);
        var compositor = new Compositor(document);
        document.ActiveLayer.Pixels.SetPixel(1, 0, Rgba.Black);
        document.ActiveLayer.Pixels.SetPixel(3, 0, Rgba.Black);

        compositor.Update(new IntRect(1, 0, 1, 1));

        Assert.Equal([Rgba.White, Rgba.Black, Rgba.White, Rgba.White], compositor.Pixels.ToArray());
    }

    [Fact]
    public void Update_IgnoresTheOffCanvasPart()
    {
        var document = Document.CreateBlank(2, 2);
        var compositor = new Compositor(document);
        document.ActiveLayer.Pixels.SetPixel(1, 1, Rgba.Black);

        compositor.Update(new IntRect(-5, -5, 20, 20));

        Assert.Equal(Rgba.Black, compositor.Pixels[3]);
    }

    [Fact]
    public void Editor_UpdatesCompositeAndReportsChange()
    {
        var editor = new DocumentEditor(Document.CreateBlank(8, 8)) { ActiveTool = new PencilTool() };
        var changes = new List<IntRect>();
        editor.CompositeChanged += (_, rect) => changes.Add(rect);

        Script.Stroke(editor, PointerButton.Left, (2, 3));

        Assert.Equal([new IntRect(2, 3, 1, 1)], changes);
        Assert.Equal(Rgba.Black, editor.Compositor.Pixels[3 * 8 + 2]);
    }

    [Fact]
    public void Editor_WithoutActiveTool_IgnoresInput()
    {
        var editor = new DocumentEditor(Document.CreateBlank(4, 4));

        Script.Stroke(editor, PointerButton.Left, (1, 1));

        Assert.Equal(Rgba.White, editor.Document.ActiveLayer.Pixels.GetPixel(1, 1));
    }
}
