using Avalonia.Controls;
using Avalonia.Input;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Tools;

/// <summary>Everything the shell needs to offer a tool in the command bar.</summary>
/// <param name="Id">Stable, culture-independent identifier.</param>
/// <param name="DisplayName">Localised name, from a <c>.resx</c>; also the button's automation name.</param>
/// <param name="IconData">Path data for a 24×24 icon.</param>
/// <param name="Shortcut">Keyboard shortcut that selects the tool, if any.</param>
/// <param name="CreateTool">Creates a fresh engine tool each time the tool is selected.</param>
/// <param name="CreateOptions">
/// Creates the tool's options panel (e.g. size), shown in the command bar while the tool is
/// active. Called once; the panel is reused, so settings it holds survive reselecting the tool.
/// </param>
public sealed record ToolDescriptor(
    string Id,
    string DisplayName,
    string IconData,
    KeyGesture? Shortcut,
    Func<ITool> CreateTool,
    Func<Control>? CreateOptions = null);
