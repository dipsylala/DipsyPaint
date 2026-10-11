using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DipsyPaint.Layout;

/// <summary>
/// Attached properties that make a control a movable <em>command group</em> (ADR-0022). A group is
/// registered by placing it, with an <see cref="IdProperty"/> and <see cref="TitleProperty"/>, in the
/// <c>MainWindow</c> edge panel where it sits by default; the XAML order is the default order.
/// </summary>
public static class CommandGroup
{
    /// <summary>Stable, culture-independent id, saved in the user's layout. Never change a shipped id.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Id", typeof(CommandGroup));

    /// <summary>Localised group name, used in the group's "Move" command and its automation name.</summary>
    public static readonly AttachedProperty<string?> TitleProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Title", typeof(CommandGroup));

    /// <summary>
    /// How the edge a group is docked to lays out: horizontal on the top and bottom edges, vertical on
    /// the left and right. Inherited, so a group's own panels can bind to it to arrange themselves.
    /// </summary>
    public static readonly AttachedProperty<Orientation> OrientationProperty =
        AvaloniaProperty.RegisterAttached<Control, Orientation>("Orientation", typeof(CommandGroup), Orientation.Horizontal, inherits: true);

    public static string? GetId(Control control) => control.GetValue(IdProperty);

    public static void SetId(Control control, string? value) => control.SetValue(IdProperty, value);

    public static string? GetTitle(Control control) => control.GetValue(TitleProperty);

    public static void SetTitle(Control control, string? value) => control.SetValue(TitleProperty, value);

    public static Orientation GetOrientation(Control control) => control.GetValue(OrientationProperty);

    public static void SetOrientation(Control control, Orientation value) => control.SetValue(OrientationProperty, value);
}
