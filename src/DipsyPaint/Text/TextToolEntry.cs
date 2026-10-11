using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Text;

/// <summary>The text tool's entry in <see cref="ToolRegistry"/>.</summary>
public static class TextToolEntry
{
    // Bespoke: a serif capital T, ADR-0012.
    private const string TextIcon = "M4 4h16v5h-1.5V5.5h-5.75v13h2.25V20h-6v-1.5h2.25v-13H5.5V9H4z";

    // Text options are app-wide, as in reference Paint: every text tool and options panel shares them.
    private static readonly TextSettings Settings = new();
    private static readonly TextRenderer Renderer = new();

    /// <summary>No shortcut: none is confirmed for reference Paint's text tool.</summary>
    public static ToolDescriptor Descriptor { get; } =
        new("text", TextStrings.Text, TextIcon, null, () => new TextTool(Settings, Renderer), () => new TextOptions(Settings));
}
