using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Files;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

/// <summary>The New image dialog's size presets, swap and resolution (ticket 41, ADR-0024).</summary>
public class NewImageDialogTests
{
    private static NewImageDialog ShowDialog()
    {
        var dialog = new NewImageDialog(Document.DefaultWidth, Document.DefaultHeight);
        dialog.Show();
        dialog.UpdateLayout();
        return dialog;
    }

    private static T Named<T>(Control parent, string name) where T : Control => parent.FindControl<T>(name)!;

    private static ComboBox SizeBox(NewImageDialog dialog) => Named<ComboBox>(dialog, "SizeBox");

    private static List<ComboBoxItem> Entries(NewImageDialog dialog) => SizeBox(dialog).Items.OfType<ComboBoxItem>().ToList();

    private static string? SelectedText(NewImageDialog dialog) => (SizeBox(dialog).SelectedItem as ComboBoxItem)?.Content as string;

    private static void Choose(NewImageDialog dialog, string text) =>
        SizeBox(dialog).SelectedItem = Entries(dialog).Single(item => (string?)item.Content == text);

    private static (decimal? Width, decimal? Height) Sides(NewImageDialog dialog) =>
        (Named<NumericUpDown>(dialog, "WidthBox").Value, Named<NumericUpDown>(dialog, "HeightBox").Value);

    private static string? ResolutionShown(NewImageDialog dialog) => Named<TextBlock>(dialog, "ResolutionValue").Text;

    private static void Click(NewImageDialog dialog, string button) =>
        Named<Button>(dialog, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Press(Window window, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static readonly Resolution Print = new(300, 300);

    /// <summary>Every entry in order: preset text with its size and resolution, a heading (no size) or Custom.</summary>
    public static TheoryData<string, int, int, double> Presets => new()
    {
        { "800 × 600 px (default)", 800, 600, 96 },
        { "1280 × 720 px (HD)", 1280, 720, 96 },
        { "1920 × 1080 px (Full HD)", 1920, 1080, 96 },
        { "2560 × 1440 px (QHD)", 2560, 1440, 96 },
        { "3840 × 2160 px (4K UHD)", 3840, 2160, 96 },
        { "1080 × 1080 px", 1080, 1080, 96 },
        { "512 × 512 px", 512, 512, 96 },
        { "256 × 256 px", 256, 256, 96 },
        { "1080 × 1920 px", 1080, 1920, 96 },
        { "2480 × 3508 px (A4, 300 dpi)", 2480, 3508, 300 },
        { "2550 × 3300 px (US Letter, 300 dpi)", 2550, 3300, 300 },
    };

    [AvaloniaFact]
    public void SizeListsThePresetsInGroups_WithHeadingsThatCantBeChosen()
    {
        var dialog = ShowDialog();

        string[] expected =
        [
            "800 × 600 px (default)",
            NewImageStrings.ScreenGroup, "1280 × 720 px (HD)", "1920 × 1080 px (Full HD)", "2560 × 1440 px (QHD)", "3840 × 2160 px (4K UHD)",
            NewImageStrings.SquareGroup, "1080 × 1080 px", "512 × 512 px", "256 × 256 px",
            NewImageStrings.PortraitGroup, "1080 × 1920 px",
            NewImageStrings.PrintGroup, "2480 × 3508 px (A4, 300 dpi)", "2550 × 3300 px (US Letter, 300 dpi)",
            NewImageStrings.Custom,
        ];
        var entries = Entries(dialog);
        Assert.Equal(expected, entries.Select(item => (string?)item.Content));
        Assert.All(entries, item => Assert.Equal((string?)item.Content, AutomationProperties.GetName(item)));

        string[] headings = [NewImageStrings.ScreenGroup, NewImageStrings.SquareGroup, NewImageStrings.PortraitGroup, NewImageStrings.PrintGroup];
        Assert.All(entries, item => Assert.Equal(!headings.Contains((string?)item.Content), item.IsEnabled));
    }

    [AvaloniaFact]
    public void OpensOn800x600At96Dpi()
    {
        var dialog = ShowDialog();

        Assert.Equal("800 × 600 px (default)", SelectedText(dialog));
        Assert.Equal((800m, 600m), Sides(dialog));
        Assert.Equal("96 dpi", ResolutionShown(dialog));
        Assert.Equal(NewImageStrings.Resolution, dialog.GetVisualDescendants().OfType<Label>().Last().Content);
    }

    [AvaloniaFact]
    public void PrefillingASizeWithoutAPreset_SelectsCustom()
    {
        var dialog = new NewImageDialog(333, 444);
        dialog.Show();

        Assert.Equal(NewImageStrings.Custom, SelectedText(dialog));
        Assert.Equal((333m, 444m), Sides(dialog));
        Assert.Equal("96 dpi", ResolutionShown(dialog));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Presets))]
    public void ChoosingAPreset_FillsInItsSizeAndResolution_AndOkReturnsThem(string text, int width, int height, double dpi)
    {
        var dialog = ShowDialog();
        Choose(dialog, NewImageStrings.Custom);
        Named<NumericUpDown>(dialog, "WidthBox").Value = 7;

        Choose(dialog, text);

        Assert.Equal((width, height), Sides(dialog));
        Assert.Equal($"{dpi} dpi", ResolutionShown(dialog));
        Assert.Equal(text, SelectedText(dialog));
        Click(dialog, "OkButton");
        Assert.Equal((width, height, new Resolution(dpi, dpi)), dialog.Result);
    }

    [AvaloniaFact]
    public void EditingWidthOrHeight_SwitchesToCustom_AndKeepsTheResolution()
    {
        var dialog = ShowDialog();
        Choose(dialog, "2480 × 3508 px (A4, 300 dpi)");

        Named<NumericUpDown>(dialog, "WidthBox").Value = 2000;

        Assert.Equal(NewImageStrings.Custom, SelectedText(dialog));
        Assert.Equal((2000m, 3508m), Sides(dialog));
        Assert.Equal("300 dpi", ResolutionShown(dialog));

        Choose(dialog, "512 × 512 px");
        Named<NumericUpDown>(dialog, "HeightBox").Value = 100;

        Assert.Equal(NewImageStrings.Custom, SelectedText(dialog));
        Assert.Equal((512m, 100m), Sides(dialog));
        Assert.Equal("96 dpi", ResolutionShown(dialog));
    }

    [AvaloniaFact]
    public void ChoosingCustom_KeepsTheCurrentValues()
    {
        var dialog = ShowDialog();
        Choose(dialog, "2550 × 3300 px (US Letter, 300 dpi)");

        Choose(dialog, NewImageStrings.Custom);

        Assert.Equal((2550m, 3300m), Sides(dialog));
        Assert.Equal("300 dpi", ResolutionShown(dialog));
        Click(dialog, "OkButton");
        Assert.Equal((2550, 3300, Print), dialog.Result);
    }

    [AvaloniaFact]
    public void Swap_FlipsWidthAndHeight_AndKeepsThePresetAndResolution()
    {
        var dialog = ShowDialog();
        Choose(dialog, "2480 × 3508 px (A4, 300 dpi)");

        Click(dialog, "SwapButton");

        Assert.Equal((3508m, 2480m), Sides(dialog));
        Assert.Equal("2480 × 3508 px (A4, 300 dpi)", SelectedText(dialog));
        Assert.Equal("300 dpi", ResolutionShown(dialog));
        Click(dialog, "OkButton");
        Assert.Equal((3508, 2480, Print), dialog.Result);
    }

    [AvaloniaFact]
    public void Swap_OnCustom_StaysCustom()
    {
        var dialog = ShowDialog();
        Named<NumericUpDown>(dialog, "WidthBox").Value = 300;

        Click(dialog, "SwapButton");

        Assert.Equal((600m, 300m), Sides(dialog));
        Assert.Equal(NewImageStrings.Custom, SelectedText(dialog));
    }

    [AvaloniaFact]
    public void EnterInASizeBox_IsOk_WithThePresetsResolution()
    {
        var dialog = ShowDialog();
        Choose(dialog, "2480 × 3508 px (A4, 300 dpi)");
        Assert.True(Named<NumericUpDown>(dialog, "HeightBox").GetVisualDescendants().OfType<TextBox>().First().Focus());

        dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal((2480, 3508, Print), dialog.Result);
    }

    [AvaloniaFact]
    public void SwapButton_IsNamedWithATooltip()
    {
        var swap = Named<Button>(ShowDialog(), "SwapButton");

        Assert.Equal(NewImageStrings.SwapSides, AutomationProperties.GetName(swap));
        Assert.Equal(NewImageStrings.SwapSides, ToolTip.GetTip(swap));
    }

    [AvaloniaFact]
    public void Dialog_IsKeyboardReachableAndNamed_ArrowsSkipHeadings_EnterIsOk_EscapeIsCancel()
    {
        var dialog = ShowDialog();
        Control[] parts =
        [
            SizeBox(dialog), Named<NumericUpDown>(dialog, "WidthBox"), Named<NumericUpDown>(dialog, "HeightBox"),
            Named<Button>(dialog, "SwapButton"), Named<Button>(dialog, "OkButton"), Named<Button>(dialog, "CancelButton"),
        ];
        Assert.All(parts, p =>
        {
            Assert.True(p.Focusable, p.Name);
            Assert.True(p.IsEffectivelyVisible, p.Name);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(p)), p.Name);
        });

        // Tab walks from Size through every part in order.
        Assert.True(parts[0].Focus());
        var focused = new List<object?> { dialog.FocusManager!.GetFocusedElement() };
        for (var i = 0; i < 8; i++)
        {
            Press(dialog, Key.Tab, PhysicalKey.Tab);
            focused.Add(dialog.FocusManager!.GetFocusedElement());
        }

        var reached = focused.OfType<Visual>()
            .Select(v => parts.FirstOrDefault(p => p == v || p.IsVisualAncestorOf(v)))
            .OfType<Control>().Distinct().ToList();
        Assert.Equal(parts, reached);

        // Down on the closed Size box chooses the next preset, stepping over the "Screen" heading.
        Assert.True(parts[0].Focus());
        Press(dialog, Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal("1280 × 720 px (HD)", SelectedText(dialog));
        Assert.Equal((1280m, 720m), Sides(dialog));

        // The dialog closes on the key press, so there is no release to send.
        dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal((1280, 720, Resolution.Default), dialog.Result);
        Assert.False(dialog.IsVisible);

        // Space still opens the Size list.
        var opened = ShowDialog();
        Assert.True(SizeBox(opened).Focus());
        Press(opened, Key.Space, PhysicalKey.Space);
        Assert.True(SizeBox(opened).IsDropDownOpen);

        var cancelled = ShowDialog();
        cancelled.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(cancelled.Result);
        Assert.False(cancelled.IsVisible);
    }
}
