using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using DipsyPaint.Colours;
using DipsyPaint.Engine;
using DipsyPaint.Layout;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class ColourEditorTests
{
    private static readonly Rgba Orange = new(255, 128, 0, 255);

    private sealed class FakeDialogs : IColourDialogs
    {
        public Rgba? Result { get; set; }
        public List<Rgba> Requests { get; } = [];

        public Task<Rgba?> EditColoursAsync(Rgba initial)
        {
            Requests.Add(initial);
            return Task.FromResult(Result);
        }
    }

    private static (MainWindow Window, ColourControl Colours, FakeDialogs Dialogs) ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var colours = window.GetLogicalDescendants().OfType<ColourControl>().Single();
        var dialogs = new FakeDialogs();
        colours.Dialogs = dialogs;
        return (window, colours, dialogs);
    }

    private static EditColoursDialog ShowDialog(Rgba initial)
    {
        var dialog = new EditColoursDialog(initial);
        dialog.Show();
        dialog.UpdateLayout();
        return dialog;
    }

    private static T Named<T>(Control parent, string name) where T : Control => parent.FindControl<T>(name)!;

    private static List<Button> CustomSwatches(ColourControl colours) =>
        Named<UniformGrid>(colours, "CustomColoursGrid").Children.OfType<Button>().ToList();

    private static void Click(Window window, Control control, MouseButton button = MouseButton.Left)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, button);
        window.MouseUp(centre, button);
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static (decimal?, decimal?, decimal?) Fields(EditColoursDialog dialog) =>
        (Named<NumericUpDown>(dialog, "Box1").Value, Named<NumericUpDown>(dialog, "Box2").Value, Named<NumericUpDown>(dialog, "Box3").Value);

    private static Color PreviewColour(EditColoursDialog dialog) => ((ISolidColorBrush)Named<Border>(dialog, "Preview").Background!).Color;

    [AvaloniaFact]
    public void Dialog_StartsFromTheGivenColour_InEveryPart()
    {
        var dialog = ShowDialog(Orange);

        Assert.Equal(ColourEditorStrings.DialogTitle, dialog.Title);
        Assert.Equal((255m, 128m, 0m), Fields(dialog));
        Assert.Equal("#FF8000", Named<TextBox>(dialog, "HexBox").Text);
        Assert.Equal(Color.FromRgb(255, 128, 0), PreviewColour(dialog));
        Assert.Equal(Color.FromRgb(255, 128, 0), Named<ColorSpectrum>(dialog, "Spectrum").HsvColor.ToRgb());
        Assert.Equal(Color.FromRgb(255, 128, 0), Named<ColorSlider>(dialog, "BrightnessSlider").HsvColor.ToRgb());
        Assert.Equal(ColorSpectrumShape.Ring, Named<ColorSpectrum>(dialog, "Spectrum").Shape);
        Assert.Equal(Orange, dialog.Colour);
    }

    [AvaloniaFact]
    public void Dialog_OkReturnsTheColour_CancelReturnsNothing()
    {
        var ok = ShowDialog(Orange);
        Named<Button>(ok, "OkButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Orange, ok.Result);
        Assert.False(ok.IsVisible);

        var cancel = ShowDialog(Orange);
        Named<Button>(cancel, "CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(cancel.Result);
        Assert.False(cancel.IsVisible);
    }

    [AvaloniaFact]
    public void Dialog_EditingTheRgbFieldsUpdatesEveryOtherPart()
    {
        var dialog = ShowDialog(Rgba.Black);

        Named<NumericUpDown>(dialog, "Box1").Value = 18;
        Named<NumericUpDown>(dialog, "Box2").Value = 52;
        Named<NumericUpDown>(dialog, "Box3").Value = 86;

        Assert.Equal(new Rgba(18, 52, 86, 255), dialog.Colour);
        Assert.Equal("#123456", Named<TextBox>(dialog, "HexBox").Text);
        Assert.Equal(Color.FromRgb(18, 52, 86), PreviewColour(dialog));
        Assert.Equal(Color.FromRgb(18, 52, 86), Named<ColorSpectrum>(dialog, "Spectrum").HsvColor.ToRgb());
    }

    [AvaloniaFact]
    public void Dialog_TypingAHexColourUpdatesEveryOtherPart_AndIgnoresIncompleteText()
    {
        var dialog = ShowDialog(Rgba.Black);
        var hex = Named<TextBox>(dialog, "HexBox");

        hex.Text = "#12";
        Assert.Equal(Rgba.Black, dialog.Colour);
        Assert.Equal("#12", hex.Text); // not rewritten while being typed

        hex.Text = "00a2e8";
        Assert.Equal(new Rgba(0, 162, 232, 255), dialog.Colour);
        Assert.Equal((0m, 162m, 232m), Fields(dialog));
        Assert.Equal("00a2e8", hex.Text);
        Assert.Null(EditColoursDialog.ParseHex("#12345G"));
        Assert.Equal(new Rgba(255, 255, 255, 255), EditColoursDialog.ParseHex(" #FFFFFF "));
    }

    [AvaloniaFact]
    public void Dialog_HsvModeRelabelsTheFieldsAndEditsHueSaturationAndValue()
    {
        var dialog = ShowDialog(new Rgba(255, 0, 0, 255));
        var model = Named<ComboBox>(dialog, "ModelBox");

        model.SelectedIndex = 1;

        Assert.Equal(
            [ColourEditorStrings.Hue, ColourEditorStrings.Saturation, ColourEditorStrings.Value],
            new[] { "Box1", "Box2", "Box3" }.Select(n => AutomationProperties.GetName(Named<NumericUpDown>(dialog, n))));
        Assert.Equal((0m, 100m, 100m), Fields(dialog));
        Assert.Equal(359m, Named<NumericUpDown>(dialog, "Box1").Maximum);

        Named<NumericUpDown>(dialog, "Box1").Value = 120;
        Named<NumericUpDown>(dialog, "Box3").Value = 50;

        Assert.Equal(new Rgba(0, 128, 0, 255), dialog.Colour);
        Assert.Equal("#008000", Named<TextBox>(dialog, "HexBox").Text);

        model.SelectedIndex = 0;
        Assert.Equal(ColourEditorStrings.Red, AutomationProperties.GetName(Named<NumericUpDown>(dialog, "Box1")));
        Assert.Equal((0m, 128m, 0m), Fields(dialog));
    }

    [AvaloniaFact]
    public void Dialog_TheBrightnessSliderChangesOnlyTheBrightness()
    {
        var dialog = ShowDialog(new Rgba(255, 0, 0, 255));
        var slider = Named<ColorSlider>(dialog, "BrightnessSlider");

        slider.Value = slider.Minimum + (slider.Maximum - slider.Minimum) / 2;

        Assert.Equal(new Rgba(128, 0, 0, 255), dialog.Colour);
        Assert.Equal(0, Named<ColorSpectrum>(dialog, "Spectrum").HsvColor.H, 3);
        Assert.Equal((128m, 0m, 0m), Fields(dialog));
    }

    [AvaloniaFact]
    public void Dialog_TheWheelPicksHueAndSaturation_WithTheMouseOrKeyboard()
    {
        var dialog = ShowDialog(new Rgba(255, 0, 0, 255));
        var spectrum = Named<ColorSpectrum>(dialog, "Spectrum");

        spectrum.HsvColor = new HsvColor(1, 240, 1, 1);
        Assert.Equal(new Rgba(0, 0, 255, 255), dialog.Colour);
        Assert.Equal("#0000FF", Named<TextBox>(dialog, "HexBox").Text);

        Assert.True(spectrum.Focus());
        var before = dialog.Colour;
        Press(dialog, Key.Left, PhysicalKey.ArrowLeft);
        Assert.NotEqual(before, dialog.Colour);
    }

    [AvaloniaFact]
    public void Dialog_IsKeyboardReachableAndNamed_EnterIsOk_EscapeIsCancel()
    {
        var dialog = ShowDialog(Orange);

        Control[] parts =
        [
            Named<ColorSpectrum>(dialog, "Spectrum"), Named<ColorSlider>(dialog, "BrightnessSlider"),
            Named<ComboBox>(dialog, "ModelBox"), Named<NumericUpDown>(dialog, "Box1"), Named<NumericUpDown>(dialog, "Box2"),
            Named<NumericUpDown>(dialog, "Box3"), Named<TextBox>(dialog, "HexBox"), Named<Button>(dialog, "OkButton"),
            Named<Button>(dialog, "CancelButton"),
        ];
        Assert.All(parts, p =>
        {
            Assert.True(p.Focusable, p.Name);
            Assert.True(p.IsEffectivelyVisible, p.Name);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(p)), p.Name);
        });

        // Tab walks from the wheel through every part in order.
        Assert.True(parts[0].Focus());
        var focused = new List<object?> { dialog.FocusManager!.GetFocusedElement() };
        for (var i = 0; i < 12; i++)
        {
            Press(dialog, Key.Tab, PhysicalKey.Tab);
            focused.Add(dialog.FocusManager!.GetFocusedElement());
        }

        var reached = focused.OfType<Visual>()
            .Select(v => parts.FirstOrDefault(p => p == v || p.IsVisualAncestorOf(v)))
            .OfType<Control>().Distinct().ToList();
        Assert.Equal(parts, reached);

        // The dialog closes on the key press, so there is no release to send.
        dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal(Orange, dialog.Result);
        Assert.False(dialog.IsVisible);

        var cancelled = ShowDialog(Orange);
        cancelled.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(cancelled.Result);
        Assert.False(cancelled.IsVisible);
    }

    [AvaloniaFact]
    public void EditColoursIsANamedKeyboardReachableButton_WithTenEmptyCustomSlots()
    {
        var (_, colours, _) = ShowWindow();
        var edit = Named<Button>(colours, "EditColoursButton");

        Assert.Equal(ColourEditorStrings.EditColours, AutomationProperties.GetName(edit));
        Assert.True(edit.Focusable && edit.IsEffectivelyVisible && edit.IsEffectivelyEnabled);
        var slots = CustomSwatches(colours);
        Assert.Equal(ColourControl.CustomColourSlots, slots.Count);
        Assert.All(slots, s =>
        {
            Assert.False(s.IsEnabled); // empty slots are skipped by Tab
            Assert.Equal(ColourEditorStrings.EmptyCustomColour, AutomationProperties.GetName(s));
        });
    }

    [AvaloniaFact]
    public void EditColoursSetsColour1AndAddsACustomColour()
    {
        var (window, colours, dialogs) = ShowWindow();
        dialogs.Result = Orange;

        Click(window, Named<Button>(colours, "EditColoursButton"));

        Assert.Equal([Rgba.Black], dialogs.Requests); // starts from the selected colour
        Assert.Equal(Orange, window.Editor.PrimaryColor);
        Assert.Equal(Rgba.White, window.Editor.SecondaryColor);
        Assert.Equal([Orange], colours.CustomColours);
        var first = CustomSwatches(colours)[0];
        Assert.True(first.IsEnabled);
        Assert.Equal(string.Format(ColourEditorStrings.CustomColour, "#FF8000"), AutomationProperties.GetName(first));
        Assert.Equal(Color.FromRgb(255, 128, 0), ((ISolidColorBrush)((Border)first.Content!).Background!).Color);
        Assert.False(CustomSwatches(colours)[1].IsEnabled);
    }

    [AvaloniaFact]
    public void WithColour2SelectedEditColoursStartsFromAndSetsColour2()
    {
        var (window, colours, dialogs) = ShowWindow();
        dialogs.Result = Orange;

        Click(window, Named<ToggleButton>(colours, "Colour2Button"));
        Click(window, Named<Button>(colours, "EditColoursButton"));

        Assert.Equal([Rgba.White], dialogs.Requests);
        Assert.Equal(Orange, window.Editor.SecondaryColor);
        Assert.Equal(Rgba.Black, window.Editor.PrimaryColor);
    }

    [AvaloniaFact]
    public void CancellingEditColoursChangesNothing()
    {
        var (window, colours, dialogs) = ShowWindow();
        dialogs.Result = null;

        Click(window, Named<Button>(colours, "EditColoursButton"));

        Assert.Single(dialogs.Requests);
        Assert.Equal(Rgba.Black, window.Editor.PrimaryColor);
        Assert.Empty(colours.CustomColours);
    }

    [AvaloniaFact]
    public void CustomColoursWorkLikePaletteSwatches()
    {
        var (window, colours, _) = ShowWindow();
        var blue = new Rgba(0, 0, 255, 255);
        colours.AddCustomColour(Orange);
        colours.AddCustomColour(blue);

        Click(window, CustomSwatches(colours)[0]);
        Click(window, CustomSwatches(colours)[1], MouseButton.Right);

        Assert.Equal(Orange, window.Editor.PrimaryColor);
        Assert.Equal(blue, window.Editor.SecondaryColor);
    }

    [AvaloniaFact]
    public void WhenTheCustomColoursAreFullTheOldestIsDropped()
    {
        var (_, colours, _) = ShowWindow();

        for (byte i = 0; i <= ColourControl.CustomColourSlots; i++)
        {
            colours.AddCustomColour(new Rgba(i, i, i, 255));
        }

        Assert.Equal(ColourControl.CustomColourSlots, colours.CustomColours.Count);
        Assert.Equal(new Rgba(1, 1, 1, 255), colours.CustomColours[0]);
        Assert.Equal(new Rgba(10, 10, 10, 255), colours.CustomColours[^1]);
        Assert.All(CustomSwatches(colours), s => Assert.True(s.IsEnabled));
    }

    [AvaloniaFact]
    public void CustomColoursAreKeptWhenTheDocumentIsReplaced()
    {
        var (window, colours, _) = ShowWindow();
        colours.AddCustomColour(Orange);

        window.DataContext = new DocumentEditor(Document.CreateBlank(32, 32));

        Assert.Equal([Orange], colours.CustomColours);
    }

    [AvaloniaFact]
    public void OnASideEdgeTheCustomColoursBecomeAThirdColumnAndStayUsable()
    {
        var (window, colours, dialogs) = ShowWindow();
        dialogs.Result = Orange;
        colours.AddCustomColour(new Rgba(0, 0, 255, 255));

        window.CommandBarLayout.Move("colours", DockEdge.Left, 0);
        window.UpdateLayout();

        var grid = Named<UniformGrid>(colours, "CustomColoursGrid");
        Assert.Equal((10, 1), (grid.Rows, grid.Columns));
        Assert.Equal(Orientation.Horizontal, Named<StackPanel>(colours, "PalettePanel").Orientation);
        var palette = Named<UniformGrid>(colours, "PaletteGrid");
        Assert.True(grid.TranslatePoint(default, window)!.Value.X >= palette.TranslatePoint(new Point(palette.Bounds.Width, 0), window)!.Value.X);

        Click(window, CustomSwatches(colours)[0]);
        Assert.Equal(new Rgba(0, 0, 255, 255), window.Editor.PrimaryColor);
        Click(window, Named<Button>(colours, "EditColoursButton"));
        Assert.Equal(Orange, window.Editor.PrimaryColor);

        window.CommandBarLayout.Move("colours", DockEdge.Top, 0);
        Assert.Equal((1, 10), (grid.Rows, grid.Columns));
        Assert.Equal(Orientation.Vertical, Named<StackPanel>(colours, "PalettePanel").Orientation);
    }
}
