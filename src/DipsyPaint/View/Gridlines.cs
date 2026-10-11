using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DipsyPaint.View;

/// <summary>
/// Gridlines over the canvas, sized to the document and laid out in document pixels. Lines run
/// along document pixel edges every <see cref="SpacingFor"/> pixels, so they keep a usable on-screen
/// spacing at any zoom and become a per-pixel grid when zoomed in far. Each line is one device pixel
/// wide. Display only: nothing here touches the document, and it takes no pointer input.
/// </summary>
public sealed class Gridlines : Control
{
    /// <summary>The least on-screen spacing between lines, in device pixels.</summary>
    public const int MinSpacing = 8;

    // Mid grey at half opacity, so lines show on light and dark pixels alike.
    private static readonly IBrush LineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80));

    private TopLevel? _topLevel;
    private ScrollViewer? _scroller;
    private double _zoom;

    public Gridlines()
    {
        IsHitTestVisible = false;
        IsVisible = false;
    }

    /// <summary>
    /// Document pixels between lines at <paramref name="zoom"/> device pixels per document pixel: the
    /// smallest power of two that puts lines at least <see cref="MinSpacing"/> device pixels apart,
    /// e.g. every pixel at 800%, every 8 pixels at 100%.
    /// </summary>
    public static int SpacingFor(double zoom)
    {
        var spacing = 1;
        while (spacing * zoom < MinSpacing && spacing < 1 << 16)
        {
            spacing *= 2;
        }

        return spacing;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        _scroller?.ScrollChanged += OnScrollChanged;
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _scroller?.ScrollChanged -= OnScrollChanged;
        _scroller = null;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var zoom = (_topLevel is null ? 1 : this.TransformToVisual(_topLevel)?.M11 ?? 1) * (_topLevel?.RenderScaling ?? 1);
        if (zoom > 0 && zoom != _zoom)
        {
            _zoom = zoom;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_zoom <= 0)
        {
            return;
        }

        // Only the lines in view, so a large document zoomed in far stays cheap to draw.
        var visible = new Rect(Bounds.Size);
        if (_scroller?.TransformToVisual(this) is { } toThis)
        {
            visible = visible.Intersect(new Rect(_scroller.Bounds.Size).TransformToAABB(toThis));
        }

        var spacing = SpacingFor(_zoom);
        var width = 1 / _zoom;
        for (var x = Math.Max(spacing, Math.Floor(visible.Left / spacing) * spacing); x < Math.Min(visible.Right, Bounds.Width); x += spacing)
        {
            context.FillRectangle(LineBrush, new Rect(x, visible.Top, width, visible.Height));
        }

        for (var y = Math.Max(spacing, Math.Floor(visible.Top / spacing) * spacing); y < Math.Min(visible.Bottom, Bounds.Height); y += spacing)
        {
            context.FillRectangle(LineBrush, new Rect(visible.Left, y, visible.Width, width));
        }
    }
}
