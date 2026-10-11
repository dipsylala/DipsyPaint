using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.View;

// Connects the magnifier tool, whenever it becomes the active tool, to the zoom.
public partial class ViewMenu
{
    private DocumentEditor? _editor;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.ActiveToolChanged -= OnActiveToolChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.ActiveToolChanged += OnActiveToolChanged;
        OnActiveToolChanged(null, EventArgs.Empty);
    }

    private void OnActiveToolChanged(object? sender, EventArgs e) =>
        (_editor?.ActiveTool as MagnifierTool)?.Magnify = Magnify;

    /// <summary>Zooms one step in (left button) or out (right button), keeping the document point under the pointer.</summary>
    private void Magnify(PointerButton button, double x, double y)
    {
        if (_zoom?.Target is not { Child: { } canvas } target)
        {
            return;
        }

        var anchor = target.FindAncestorOfType<ScrollViewer>() is { } scroller ? canvas.TranslatePoint(new Point(x, y), scroller) : null;
        if (button == PointerButton.Right)
        {
            _zoom.ZoomOut(anchor);
        }
        else
        {
            _zoom.ZoomIn(anchor);
        }
    }
}
