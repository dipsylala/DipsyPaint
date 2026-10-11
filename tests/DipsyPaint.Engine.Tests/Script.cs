using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>Drives an editor with scripted pointer input.</summary>
internal static class Script
{
    /// <summary>Presses at the first point, moves through the rest, and releases at the last.</summary>
    public static void Stroke(DocumentEditor editor, PointerButton button, params (double X, double Y)[] points)
    {
        editor.Apply(new PointerInput(PointerAction.Down, points[0].X, points[0].Y, button));
        foreach (var (x, y) in points.Skip(1))
        {
            editor.Apply(new PointerInput(PointerAction.Move, x, y, button));
        }

        editor.Apply(new PointerInput(PointerAction.Up, points[^1].X, points[^1].Y, button));
    }

    /// <summary>
    /// Switches to the selection tool <paramref name="mode"/> (ADR-0025), with <paramref name="settings"/>
    /// (default the editor's); the selection is kept.
    /// </summary>
    public static void Use(DocumentEditor editor, SelectionToolMode mode, SelectionSettings? settings = null) =>
        editor.ActiveTool = new SelectionTool(settings ?? editor.SelectionSettings) { Mode = mode };
}
