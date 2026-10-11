using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools.Brushes;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>The airbrush, oil brush and watercolour brush in the Brush tool's Brushes menu (ticket 29).</summary>
public class ArtisticBrushes2Tests
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

    private static List<string?> MenuNames(Window window) => [.. BrushMenu(window).Items.Cast<ComboBoxItem>().Select(AutomationProperties.GetName)];

    private static void Choose(Window window, string name)
    {
        BrushMenu(window).SelectedIndex = MenuNames(window).IndexOf(name);
        window.UpdateLayout();
    }

    private static Slider Slider(Window window, string name) =>
        window.GetVisualDescendants().OfType<Slider>().Single(s => AutomationProperties.GetName(s) == name && s.IsEffectivelyVisible);

    private static Point At(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static List<(int X, int Y, Rgba Color)> Painted(MainWindow window)
    {
        var pixels = window.Document.ActiveLayer.Pixels;
        return [.. from y in Enumerable.Range(0, pixels.Height)
                   from x in Enumerable.Range(0, pixels.Width)
                   let color = pixels.GetPixel(x, y)
                   where color != Rgba.White
                   select (x, y, color)];
    }

    /// <summary>The brush choice and brush settings are app-wide, so tests put them back.</summary>
    private static void Restore(Window window)
    {
        Slider(window, BrushStrings.Size).Value = 5;
        Slider(window, OpacityStrings.Opacity).Value = 100;
        ToolRegistry.Brushes.Selected = ToolRegistry.Brushes.Brushes[0];
    }

    [AvaloniaFact]
    public void TheBrushesMenu_OffersTheAirbrushOilAndWatercolourBrushes_InReferencePaintsOrder()
    {
        var window = ShowWindow();

        var names = MenuNames(window);

        Assert.True(BrushMenu(window).IsEffectivelyVisible);
        int brush = names.IndexOf(BrushStrings.Brush), airbrush = names.IndexOf(ArtisticBrushes2Strings.Airbrush);
        int oil = names.IndexOf(ArtisticBrushes2Strings.OilBrush), watercolour = names.IndexOf(ArtisticBrushes2Strings.WatercolourBrush);
        Assert.True(brush == 0 && brush < airbrush && airbrush < oil && oil < watercolour, string.Join(", ", names));
        Assert.Equal(names.Count - 1, watercolour); // last, as in reference Paint
    }

    [AvaloniaTheory]
    [InlineData("airbrush", typeof(AirbrushTool))]
    [InlineData("oil-brush", typeof(OilBrushTool))]
    [InlineData("watercolour-brush", typeof(WatercolourBrushTool))]
    public void ChoosingABrush_PaintsWithIt_AtTheSetSizeAndOpacity(string id, Type tool)
    {
        var window = ShowWindow();
        try
        {
            var name = ToolRegistry.Brushes.Brushes.Single(b => b.Id == id).DisplayName;
            Choose(window, name);
            Assert.IsType(tool, window.Editor.ActiveTool);
            Slider(window, BrushStrings.Size).Value = 10;
            Slider(window, OpacityStrings.Opacity).Value = 50;

            window.MouseDown(At(window, 10, 24), MouseButton.Left);
            for (var x = 12; x <= 54; x += 2)
            {
                window.MouseMove(At(window, x, 24));
            }

            window.MouseUp(At(window, 54, 24), MouseButton.Left);

            var painted = Painted(window);
            Assert.NotEmpty(painted);
            Assert.All(painted, p => Assert.InRange(p.Y, 24 - 6, 24 + 6));      // size 10
            Assert.All(painted, p => Assert.True(p.Color.R >= 126, $"{p}")); // black at 50 % at most
        }
        finally
        {
            Restore(window);
        }
    }

    [AvaloniaFact]
    public void TheAirbrush_KeepsSprayingWhileThePointerIsHeldStill_UntilReleased()
    {
        var window = ShowWindow();
        var clock = new ManualClock();
        window.FindControl<DocumentCanvas>("DocumentView")!.Clock = clock;
        try
        {
            Choose(window, ArtisticBrushes2Strings.Airbrush);
            Slider(window, BrushStrings.Size).Value = 30;

            void Wait(int milliseconds)
            {
                for (var elapsed = 0; elapsed < milliseconds; elapsed += 8)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(8));
                    Dispatcher.UIThread.RunJobs();
                }
            }

            window.MouseDown(At(window, 32, 24), MouseButton.Left);
            var pressed = Painted(window).Count;
            Wait(300);
            var held = Painted(window).Count;
            window.MouseUp(At(window, 32, 24), MouseButton.Left);
            var released = Painted(window).Count;
            Wait(300);

            Assert.True(held > pressed * 3, $"{pressed} dots at the press, {held} after holding still");
            Assert.Equal(released, Painted(window).Count);
            window.Editor.Undo(); // one history step for the whole stroke
            Assert.Empty(Painted(window));
            Assert.False(window.Editor.History.CanUndo);
        }
        finally
        {
            Restore(window);
        }
    }
}
