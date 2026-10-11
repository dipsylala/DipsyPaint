using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>The textured outline and fill styles in the shape options (ticket 37).</summary>
public class TexturedShapeTests
{
    private static MainWindow ShowWindowWithShapes()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48)) { Width = 1100 };
        window.Show();
        window.UpdateLayout();
        Click(window, ShapeStrings.Shapes);
        window.UpdateLayout();
        Click(window, ShapeStrings.Rectangle);
        window.UpdateLayout();
        return window;
    }

    private static ShapeOptions Options(Window window) => window.GetLogicalDescendants().OfType<ShapeOptions>().Single();

    private static ComboBox Box(Window window, string name) => Options(window).FindControl<ComboBox>(name)!;

    private static void Click(Window window, string buttonName) =>
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == buttonName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Rect RectOf(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    [AvaloniaFact]
    public void OutlineAndFill_OfferReferencePaintsStyles_InItsOrder()
    {
        var window = ShowWindowWithShapes();
        string[] textured =
        [
            TexturedShapeStrings.Crayon, TexturedShapeStrings.Marker, TexturedShapeStrings.Oil,
            TexturedShapeStrings.NaturalPencil, TexturedShapeStrings.Watercolour,
        ];

        static IEnumerable<string> Items(ComboBox box) => box.Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!);

        Assert.Equal([ShapeStrings.NoOutline, ShapeStrings.SolidColor, .. textured], Items(Box(window, "OutlineBox")));
        Assert.Equal([ShapeStrings.NoFill, ShapeStrings.SolidColor, .. textured], Items(Box(window, "FillBox")));
        Assert.Equal(Enum.GetValues<ShapeStyle>().Length, Box(window, "OutlineBox").ItemCount); // the boxes map styles by index
    }

    [AvaloniaFact]
    public void ChoosingATexturedStyle_RedrawsThePendingShape_WithoutMovingTheCanvas()
    {
        var window = ShowWindowWithShapes();
        var (outlineBox, fillBox) = (Box(window, "OutlineBox"), Box(window, "FillBox"));
        var (outline, fill) = (outlineBox.SelectedIndex, fillBox.SelectedIndex);
        try
        {
            outlineBox.SelectedIndex = (int)ShapeStyle.Solid;
            fillBox.SelectedIndex = (int)ShapeStyle.Solid;
            window.UpdateLayout();
            var picker = window.GetVisualDescendants().OfType<ToolPicker>().Single();
            var buttons = picker.FindControl<WrapPanel>("ToolButtons")!;
            var rowHeight = buttons.Children[0].Bounds.Height + buttons.Margin.Top + buttons.Margin.Bottom;
            var canvasArea = RectOf(window.FindControl<ScrollViewer>("CanvasScroll")!, window);

            window.Editor.SecondaryColor = new Rgba(0, 0, 255, 255); // the fill, white by default
            window.MouseDown(InWindow(window, 4.5, 4.5), MouseButton.Left);
            window.MouseMove(InWindow(window, 50.5, 40.5));
            window.MouseUp(InWindow(window, 50.5, 40.5), MouseButton.Left);
            Assert.True(window.Editor.HasPending);
            var solid = window.Editor.Compositor.Pixels.ToArray();

            outlineBox.SelectedIndex = (int)ShapeStyle.NaturalPencil; // the widest name
            window.UpdateLayout();
            var outlined = window.Editor.Compositor.Pixels.ToArray();
            Assert.NotEqual(solid, outlined);

            fillBox.SelectedIndex = (int)ShapeStyle.NaturalPencil;
            window.UpdateLayout();
            Assert.NotEqual(outlined, window.Editor.Compositor.Pixels.ToArray());
            Assert.True(window.Editor.HasPending);
            Assert.False(window.Editor.History.CanUndo);

            Assert.True(canvasArea == RectOf(window.FindControl<ScrollViewer>("CanvasScroll")!, window), "the canvas moved");
            Assert.True(picker.Bounds.Height <= rowHeight + 0.5, $"the tools group is {picker.Bounds.Height} px tall, its button row {rowHeight}");
        }
        finally
        {
            // The shape settings are app-wide.
            outlineBox.SelectedIndex = outline;
            fillBox.SelectedIndex = fill;
        }
    }
}
