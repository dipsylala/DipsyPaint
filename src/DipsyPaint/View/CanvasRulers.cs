using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DipsyPaint.View;

/// <summary>
/// Holds the canvas area and, while <see cref="IsShown"/>, draws reference Paint's rulers along its
/// top and left edges: marks in document pixels, starting at 0 on the canvas's top-left corner,
/// that follow zoom and scrolling, plus a mark at the pointer. Display only: nothing here touches
/// the document.
/// </summary>
public sealed class CanvasRulers : Decorator
{
    /// <summary>The rulers' thickness, in device-independent pixels.</summary>
    public const double Thickness = 20;

    // The least on-screen space between numbered marks and between any marks, in device-independent pixels.
    private const double MinLabelSpacing = 50;
    private const double MinMarkSpacing = 4;

    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<CanvasRulers, bool>(nameof(IsShown));

    public static readonly StyledProperty<Visual?> CanvasProperty =
        AvaloniaProperty.Register<CanvasRulers, Visual?>(nameof(Canvas));

    private Matrix? _mapping;
    private Point? _pointer;

    public CanvasRulers()
    {
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => MovePointer(null), RoutingStrategies.Direct, handledEventsToo: true);
    }

    /// <summary>Whether the rulers show; the child gets the rest of the space.</summary>
    public bool IsShown
    {
        get => GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    /// <summary>The element whose top-left corner is document pixel (0, 0) and whose units are document pixels.</summary>
    public Visual? Canvas
    {
        get => GetValue(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>
    /// The document pixel spacing between numbered marks and between all marks at <paramref name="scale"/>
    /// on-screen pixels per document pixel: numbered marks at 1, 2 or 5 × 10ⁿ, at least 50 apart on
    /// screen; smaller marks splitting them into 10, 5 or 2 whole pixels, at least 4 apart on screen.
    /// </summary>
    public static (int Major, int Minor) StepsFor(double scale)
    {
        var major = 1;
        for (var i = 0; major * scale < MinLabelSpacing && major < 1_000_000; i++)
        {
            major = (i % 3) switch { 0 => major * 2, 1 => major / 2 * 5, _ => major * 2 };
        }

        foreach (var parts in (int[])[10, 5, 2])
        {
            if (major % parts == 0 && major / parts * scale >= MinMarkSpacing)
            {
                return (major, major / parts);
            }
        }

        return (major, major);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsShownProperty)
        {
            Padding = IsShown ? new Thickness(Thickness, Thickness, 0, 0) : default;
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    // Zooming and scrolling both re-arrange the canvas, so a layout pass is when the marks may move.
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var mapping = Canvas?.TransformToVisual(this);
        if (mapping != _mapping)
        {
            _mapping = mapping;
            InvalidateVisual();
        }
    }

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e) => MovePointer(e.GetPosition(this));

    private void MovePointer(Point? pointer)
    {
        _pointer = pointer;
        if (IsShown)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!IsShown)
        {
            return;
        }

        var size = Bounds.Size;
        var background = Resource("SystemControlBackgroundAltHighBrush", Brushes.White);
        var foreground = Resource("SystemControlForegroundBaseMediumHighBrush", Brushes.DimGray);
        var pen = new Pen(foreground, 1);
        var top = new Rect(Thickness, 0, Math.Max(0, size.Width - Thickness), Thickness);
        var left = new Rect(0, Thickness, Thickness, Math.Max(0, size.Height - Thickness));
        context.FillRectangle(background, new Rect(0, 0, size.Width, Thickness));
        context.FillRectangle(background, left);
        context.DrawLine(pen, new Point(0, Thickness - 0.5), new Point(size.Width, Thickness - 0.5));
        context.DrawLine(pen, new Point(Thickness - 0.5, 0), new Point(Thickness - 0.5, size.Height));
        if (_mapping is not { } mapping || mapping.M11 <= 0)
        {
            return;
        }

        using (context.PushClip(top))
        {
            DrawMarks(context, mapping.M31, mapping.M11, size.Width, horizontal: true, pen, foreground);
            if (_pointer is { } p)
            {
                context.DrawLine(pen, new Point(Snap(p.X), 0), new Point(Snap(p.X), Thickness));
            }
        }

        using (context.PushClip(left))
        {
            DrawMarks(context, mapping.M32, mapping.M11, size.Height, horizontal: false, pen, foreground);
            if (_pointer is { } p)
            {
                context.DrawLine(pen, new Point(0, Snap(p.Y)), new Point(Thickness, Snap(p.Y)));
            }
        }
    }

    // Marks for document pixels 0 and up at origin + value × scale along one ruler, from its outer
    // edge inwards: full length and numbered, half length halfway between, quarter length otherwise.
    private void DrawMarks(DrawingContext context, double origin, double scale, double end, bool horizontal, IPen pen, IBrush foreground)
    {
        var (major, minor) = StepsFor(scale);
        var first = Math.Max(0, (int)Math.Ceiling((Thickness - origin) / scale / minor)) * minor;
        for (long value = first; origin + value * scale <= end; value += minor)
        {
            var at = Snap(origin + value * scale);
            var length = value % major == 0 ? Thickness
                : major / minor % 2 == 0 && value % (major / 2) == 0 ? Thickness / 2
                : Thickness / 4;
            if (horizontal)
            {
                context.DrawLine(pen, new Point(at, Thickness - length), new Point(at, Thickness));
            }
            else
            {
                context.DrawLine(pen, new Point(Thickness - length, at), new Point(Thickness, at));
            }

            if (value % major == 0)
            {
                var label = new FormattedText(
                    value.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 10, foreground);
                // Numbers read along the ruler, just past their mark; the left ruler's are turned a quarter left.
                var transform = horizontal
                    ? Matrix.CreateTranslation(at + 2, 0)
                    : Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(0, at + 2 + label.Width);
                using (context.PushTransform(transform))
                {
                    context.DrawText(label, default);
                }
            }
        }
    }

    // The centre of the device-independent pixel that holds the position, so a 1-pixel line is crisp.
    private static double Snap(double position) => Math.Floor(position) + 0.5;

    private IBrush Resource(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;
}
