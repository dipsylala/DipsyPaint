using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class ColourTests
{
    private static readonly Rgba Red = new(237, 28, 36, 255);

    private static MainWindow ShowWindow(int width = 64, int height = 48)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static ColourControl Colours(Window window) => window.GetLogicalDescendants().OfType<ColourControl>().Single();

    private static Button Swatch(Window window, string name) =>
        Colours(window).GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    private static T Named<T>(Window window, string name) where T : Control => Colours(window).FindControl<T>(name)!;

    private static Color SwatchColour(Window window, string swatchName) =>
        ((ISolidColorBrush)Named<Border>(window, swatchName).Background!).Color;

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Click(Window window, Control control, MouseButton button = MouseButton.Left)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, button);
        window.MouseUp(centre, button);
    }

    private static ToggleButton ToolButton(Window window, string name) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name);

    [AvaloniaFact]
    public void ShowsReferencePaintsDefaultPaletteWithNamedKeyboardReachableSwatches()
    {
        var window = ShowWindow();

        var swatches = Colours(window).FindControl<UniformGrid>("PaletteGrid")!.Children.OfType<Button>().ToList();

        Assert.Equal(20, swatches.Count);
        Assert.Equal(ColourStrings.Black, AutomationProperties.GetName(swatches[0]));
        Assert.Equal(ColourStrings.White, AutomationProperties.GetName(swatches[10]));
        Assert.All(swatches, s =>
        {
            Assert.True(s.Focusable);
            Assert.True(s.IsEffectivelyVisible);
        });
        Assert.Equal(new Rgba(237, 28, 36, 255), ColourControl.Palette.Single(p => p.Name == ColourStrings.Red).Color);
    }

    [AvaloniaFact]
    public void StartsWithBlackAndWhite()
    {
        var window = ShowWindow();

        Assert.Equal(Colors.Black, SwatchColour(window, "Colour1Swatch"));
        Assert.Equal(Colors.White, SwatchColour(window, "Colour2Swatch"));
        Assert.True(Named<ToggleButton>(window, "Colour1Button").IsChecked);
    }

    [AvaloniaFact]
    public void LeftClickOnASwatchSetsColour1AndRightClickSetsColour2()
    {
        var window = ShowWindow();

        Click(window, Swatch(window, ColourStrings.Red));
        Click(window, Swatch(window, ColourStrings.Green), MouseButton.Right);

        Assert.Equal(Red, window.Editor.PrimaryColor);
        Assert.Equal(new Rgba(34, 177, 76, 255), window.Editor.SecondaryColor);
        Assert.Equal(Color.FromRgb(237, 28, 36), SwatchColour(window, "Colour1Swatch"));
        Assert.Equal(Color.FromRgb(34, 177, 76), SwatchColour(window, "Colour2Swatch"));
    }

    [AvaloniaFact]
    public void WithColour2SelectedLeftClickOnASwatchSetsColour2()
    {
        var window = ShowWindow();

        Click(window, Named<ToggleButton>(window, "Colour2Button"));
        Click(window, Swatch(window, ColourStrings.Red));

        Assert.True(Named<ToggleButton>(window, "Colour2Button").IsChecked);
        Assert.False(Named<ToggleButton>(window, "Colour1Button").IsChecked);
        Assert.Equal(Red, window.Editor.SecondaryColor);
        Assert.Equal(Rgba.Black, window.Editor.PrimaryColor);
    }

    [AvaloniaFact]
    public void SwapExchangesColour1AndColour2()
    {
        var window = ShowWindow();
        window.Editor.PrimaryColor = Red;

        Click(window, Named<Button>(window, "SwapButton"));

        Assert.Equal(Rgba.White, window.Editor.PrimaryColor);
        Assert.Equal(Red, window.Editor.SecondaryColor);
        Assert.Equal(Colors.White, SwatchColour(window, "Colour1Swatch"));
        Assert.Equal(Color.FromRgb(237, 28, 36), SwatchColour(window, "Colour2Swatch"));
    }

    [AvaloniaFact]
    public void PencilDrawsWithTheChosenColours()
    {
        var window = ShowWindow();
        Click(window, Swatch(window, ColourStrings.Red));
        Click(window, Swatch(window, ColourStrings.Indigo), MouseButton.Right);

        window.MouseDown(InWindow(window, 10.5, 10.5), MouseButton.Left);
        window.MouseUp(InWindow(window, 10.5, 10.5), MouseButton.Left);
        window.MouseDown(InWindow(window, 20.5, 10.5), MouseButton.Right);
        window.MouseUp(InWindow(window, 20.5, 10.5), MouseButton.Right);

        var layer = window.Document.ActiveLayer.Pixels;
        Assert.Equal(Red, layer.GetPixel(10, 10));
        Assert.Equal(new Rgba(63, 72, 204, 255), layer.GetPixel(20, 10));
    }

    [AvaloniaFact]
    public void ColourPickerSetsColoursFromTheCanvasAndReturnsToThePreviousTool()
    {
        var window = ShowWindow();
        var layer = window.Document.ActiveLayer.Pixels;
        layer.SetPixel(5, 5, Red);
        layer.SetPixel(6, 5, new Rgba(0, 162, 232, 255));
        window.Editor.Compositor.Update(new IntRect(5, 5, 2, 1));
        var pencil = ToolButton(window, ToolStrings.Pencil);
        var picker = ToolButton(window, ColourStrings.ColourPicker);

        Click(window, picker);
        Assert.IsType<ColorPickerTool>(window.Editor.ActiveTool);
        window.MouseDown(InWindow(window, 5.5, 5.5), MouseButton.Left);
        window.MouseUp(InWindow(window, 5.5, 5.5), MouseButton.Left);

        Assert.Equal(Red, window.Editor.PrimaryColor);
        Assert.Equal(Color.FromRgb(237, 28, 36), SwatchColour(window, "Colour1Swatch"));
        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
        Assert.True(pencil.IsChecked);
        Assert.False(picker.IsChecked);

        Click(window, picker);
        window.MouseDown(InWindow(window, 6.5, 5.5), MouseButton.Right);
        window.MouseUp(InWindow(window, 6.5, 5.5), MouseButton.Right);

        Assert.Equal(new Rgba(0, 162, 232, 255), window.Editor.SecondaryColor);
        Assert.Equal(Color.FromRgb(0, 162, 232), SwatchColour(window, "Colour2Swatch"));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ColoursAreKeptWhenTheDocumentIsReplaced()
    {
        var window = ShowWindow();
        Click(window, Swatch(window, ColourStrings.Red));

        window.DataContext = new DocumentEditor(Document.CreateBlank(8, 8));

        Assert.Equal(Red, ((DocumentEditor)window.DataContext).PrimaryColor);
        Assert.Equal(Color.FromRgb(237, 28, 36), SwatchColour(window, "Colour1Swatch"));
    }
}
