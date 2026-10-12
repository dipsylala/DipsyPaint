using Avalonia;
using Avalonia.Controls;

namespace DipsyPaint.Layout;

/// <summary>
/// Lays out the undocked command groups over the canvas area (ADR-0026). Each child sits at its
/// <see cref="PositionProperty"/>, measured from the panel's top-left corner, pulled back inside the
/// panel when the panel is too small for it (e.g. after the window shrinks). The stored position is
/// kept, so the group returns there when the panel grows again.
/// </summary>
public sealed class UndockedGroupsPanel : Panel
{
    public static readonly AttachedProperty<Point> PositionProperty =
        AvaloniaProperty.RegisterAttached<UndockedGroupsPanel, Control, Point>("Position");

    static UndockedGroupsPanel()
    {
        AffectsParentArrange<UndockedGroupsPanel>(PositionProperty);
    }

    public static Point GetPosition(Control control) => control.GetValue(PositionProperty);

    public static void SetPosition(Control control, Point value) => control.SetValue(PositionProperty, value);

    /// <summary>Where a child of <paramref name="size"/> at <paramref name="position"/> is shown in a panel of <paramref name="panel"/> size.</summary>
    public static Point Clamp(Point position, Size size, Size panel) => new(
        Math.Clamp(position.X, 0, Math.Max(0, panel.Width - size.Width)),
        Math.Clamp(position.Y, 0, Math.Max(0, panel.Height - size.Height)));

    // Children are measured against the panel, so groups that wrap (e.g. the tool buttons) wrap to it.
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(Clamp(GetPosition(child), child.DesiredSize, finalSize), child.DesiredSize));
        }

        return finalSize;
    }
}
