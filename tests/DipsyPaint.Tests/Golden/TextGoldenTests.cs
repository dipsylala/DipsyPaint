using Avalonia.Headless.XUnit;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Text;

namespace DipsyPaint.Tests.Golden;

/// <summary>
/// Committed text, through the real renderer (<see cref="TextRenderer"/>), in Inter, a font bundled
/// with the tests, so no system font is involved. Compared with <see cref="TextGoldenImage"/>'s
/// cross-OS tolerance.
/// </summary>
public class TextGoldenTests
{
    private const string Inter = "fonts:Inter#Inter";
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static (DocumentEditor Editor, TextTool Tool, TextSettings Settings) CreateEditor(int width, int height)
    {
        var settings = new TextSettings { FontFamily = Inter, Size = 12 };
        var tool = new TextTool(settings, new TextRenderer());
        var editor = new DocumentEditor(Document.CreateBlank(width, height)) { ActiveTool = tool };
        return (editor, tool, settings);
    }

    /// <summary>Opens a box by dragging from (x, y) to (right, bottom), types the text and commits it.</summary>
    private static void AddText(DocumentEditor editor, TextTool tool, int x, int y, int right, int bottom, string text)
    {
        editor.Apply(new PointerInput(PointerAction.Down, x + 0.5, y + 0.5));
        editor.Apply(new PointerInput(PointerAction.Move, right + 0.5, bottom + 0.5));
        editor.Apply(new PointerInput(PointerAction.Up, right + 0.5, bottom + 0.5));
        tool.Text = text;
        editor.RefreshPending();
        editor.CommitPending();
    }

    [AvaloniaFact]
    public void Styles()
    {
        var (editor, tool, settings) = CreateEditor(200, 140);
        var styles = new (string Text, Action<bool> Set)[]
        {
            ("Regular Hamburg 0123", _ => { }),
            ("Bold Hamburg 0123", on => settings.Bold = on),
            ("Italic Hamburg 0123", on => settings.Italic = on),
            ("Underline Hamburg 0123", on => settings.Underline = on),
            ("Strikethrough Hamburg", on => settings.Strikethrough = on),
        };

        for (var i = 0; i < styles.Length; i++)
        {
            styles[i].Set(true);
            AddText(editor, tool, 4, 4 + i * 22, 195, 4 + i * 22, styles[i].Text);
            styles[i].Set(false);
        }

        settings.Bold = settings.Italic = settings.Underline = settings.Strikethrough = true;
        AddText(editor, tool, 4, 114, 195, 114, "All four Hamburg");

        TextGoldenImage.AssertMatches("text-styles", editor.Compositor);
    }

    [AvaloniaFact]
    public void SizesAndWrapping()
    {
        var (editor, tool, settings) = CreateEditor(240, 170);

        settings.Size = 8;
        AddText(editor, tool, 4, 4, 83, 4, "The quick brown fox jumps over the lazy dog.");
        settings.Size = 20;
        AddText(editor, tool, 92, 4, 235, 4, "The quick brown fox jumps\nover the lazy dog.");

        TextGoldenImage.AssertMatches("text-sizes-wrap", editor.Compositor);
    }

    [AvaloniaFact]
    public void ColoursAndBackground()
    {
        var (editor, tool, settings) = CreateEditor(200, 80);
        editor.PrimaryColor = Red;
        editor.SecondaryColor = Blue;

        settings.Size = 16;
        settings.FillBackground = true;
        AddText(editor, tool, 4, 4, 120, 40, "Filled box");
        settings.FillBackground = false;
        AddText(editor, tool, 60, 30, 195, 30, "Transparent over it"); // anti-aliased edges blend over blue and white

        TextGoldenImage.AssertMatches("text-colours", editor.Compositor);
    }
}
