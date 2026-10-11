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
using DipsyPaint.Engine.Tools.Brushes;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>Ticket 28: calligraphy brush, calligraphy pen, crayon, marker and natural pencil in the Brushes menu.</summary>
public class ArtisticBrushes1Tests
{
    private static MainWindow ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == BrushStrings.Brush)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        return window;
    }

    private static ComboBox BrushMenu(Window window) =>
        window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == BrushStrings.Brushes);

    private static Slider Slider(Window window, string name) =>
        window.GetVisualDescendants().OfType<Slider>().Single(s => AutomationProperties.GetName(s) == name && s.IsEffectivelyVisible);

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        Point At((double X, double Y) p) => canvas.TranslatePoint(new Point(p.X, p.Y), window)!.Value;
        window.MouseDown(At(from), MouseButton.Left);
        window.MouseMove(At(to));
        window.MouseUp(At(to), MouseButton.Left);
    }

    /// <summary>The brush choice and settings are app-wide, so tests put them back.</summary>
    private static void Reset(Window window)
    {
        BrushMenu(window).SelectedIndex = 0;
        Slider(window, BrushStrings.Size).Value = 5;
        Slider(window, OpacityStrings.Opacity).Value = 100;
    }

    [AvaloniaFact]
    public void TheBrushesMenu_OffersTheFiveBrushes_InReferencePaintsOrder()
    {
        var window = ShowWindow();

        var names = BrushMenu(window).Items.Cast<ComboBoxItem>().Select(AutomationProperties.GetName).ToList();

        string[] expected =
        [
            BrushStrings.Brush, ArtisticBrushes1Strings.CalligraphyBrush, ArtisticBrushes1Strings.CalligraphyPen,
            ArtisticBrushes1Strings.Crayon, ArtisticBrushes1Strings.Marker, ArtisticBrushes1Strings.NaturalPencil,
        ];
        // Other brushes may sit between these (ticket 29), but these keep their relative order.
        Assert.Equal(expected, names.Where(expected.Contains));
        Assert.Equal(["Calligraphy brush", "Calligraphy pen", "Crayon", "Marker", "Natural pencil"], expected[1..]);
    }

    [AvaloniaTheory]
    [InlineData("calligraphy-brush")]
    [InlineData("calligraphy-pen")]
    [InlineData("crayon")]
    [InlineData("marker")]
    [InlineData("natural-pencil")]
    public void ChoosingABrush_PaintsWithIt_AtTheSetSizeAndOpacity(string id)
    {
        var window = ShowWindow();
        try
        {
            var menu = BrushMenu(window);
            menu.SelectedItem = menu.Items.Cast<ComboBoxItem>().Single(i => ((BrushDescriptor)i.Tag!).Id == id);
            Slider(window, BrushStrings.Size).Value = 20;
            Slider(window, OpacityStrings.Opacity).Value = 50;

            Drag(window, (10, 24), (54, 24));

            var tool = Assert.IsType<ArtisticBrushTool>(window.Editor.ActiveTool);
            Assert.Same(Expected(id), tool.Tip);
            var pixels = window.Document.ActiveLayer.Pixels;
            var painted = Enumerable.Range(0, 64 * 48).Select(i => (X: i % 64, Y: i / 64, P: pixels.GetPixel(i % 64, i / 64)))
                .Where(p => p.P != Rgba.White).ToList();
            Assert.NotEmpty(painted);
            Assert.All(painted, p => Assert.True(p.P.R >= 127, $"({p.X}, {p.Y}) is {p.P}, darker than 50 % black"));
            Assert.Contains(painted, p => Math.Abs(p.Y - 24) >= 5); // wider than the default size of 5
        }
        finally
        {
            Reset(window);
        }

        Assert.Equal("brush", ToolRegistry.Brushes.Selected.Id);
    }

    private static BrushTip Expected(string id) => id switch
    {
        "calligraphy-brush" => BrushTip.CalligraphyBrush,
        "calligraphy-pen" => BrushTip.CalligraphyPen,
        "crayon" => BrushTip.Crayon,
        "marker" => BrushTip.Marker,
        _ => BrushTip.NaturalPencil,
    };
}
