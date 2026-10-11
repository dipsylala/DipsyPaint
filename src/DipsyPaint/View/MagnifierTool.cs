using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.View;

/// <summary>
/// Reference Paint's magnifier: a click zooms in one step on the clicked point, a right-click zooms
/// out. Zoom is the shell's job, so the tool only reports the click through <see cref="Magnify"/>
/// (the View menu connects it); it changes no pixels.
/// </summary>
public sealed class MagnifierTool : ITool
{
    // Bespoke: a magnifying glass, ADR-0012.
    private const string Icon =
        "M10 2.5a7.5 7.5 0 0 1 5.92 12.1l5.36 5.36-1.06 1.06-5.36-5.36A7.5 7.5 0 1 1 10 2.5zm0 1.5a6 6 0 1 0 0 12a6 6 0 1 0 0-12z";

    /// <summary>The magnifier's entry in <see cref="ToolRegistry"/>. No shortcut: none is confirmed for reference Paint.</summary>
    public static ToolDescriptor Descriptor { get; } = new("magnifier", ViewStrings.Magnifier, Icon, null, () => new MagnifierTool());

    /// <summary>Called on each press with the button and the document point under the pointer.</summary>
    public Action<PointerButton, double, double>? Magnify { get; set; }

    public IntRect Handle(ToolContext context, PointerInput input)
    {
        if (input.Action == PointerAction.Down)
        {
            Magnify?.Invoke(input.Button, input.X, input.Y);
        }

        return IntRect.Empty;
    }
}
