using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
using DipsyPaint.Text;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

public class TextTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    /// <summary>Shows a window with the text tool active, in the bundled Inter font, 12 pt, no styles.</summary>
    private static MainWindow ShowWindowWithTextTool()
    {
        var window = new MainWindow(Document.CreateBlank(320, 200));
        window.Show();
        window.UpdateLayout();
        Click(window, TextStrings.Text);
        window.UpdateLayout();
        var settings = Tool(window).Settings;
        settings.FontFamily = "fonts:Inter#Inter";
        settings.Size = 12;
        settings.Bold = settings.Italic = settings.Underline = settings.Strikethrough = settings.FillBackground = false;
        return window;
    }

    private static TextTool Tool(MainWindow window) => Assert.IsType<TextTool>(window.Editor.ActiveTool);

    private static TextBox Box(Window window) => window.FindControl<TextEntry>("TextEntry")!.FindControl<TextBox>("Box")!;

    private static TextOptions Options(Window window) => window.GetLogicalDescendants().OfType<TextOptions>().Single();

    private static void Click(Window window, string buttonName) =>
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == buttonName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
        window.UpdateLayout();
    }

    private static void ClickCanvas(Window window, double x, double y) => Drag(window, (x, y), (x, y));

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    // As a keyboard does: a key down, the text it produces, a key up.
    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            var key = char.IsAsciiLetter(c) ? Key.A + (char.ToUpperInvariant(c) - 'A') : Key.None;
            window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, c.ToString());
            window.KeyTextInput(c.ToString());
            window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, c.ToString());
        }

        window.UpdateLayout();
    }

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static int CountLayerPixels(MainWindow window, IntRect area, Func<Rgba, bool> match) =>
        CountPixels(area, (x, y) => match(window.Document.ActiveLayer.Pixels.GetPixel(x, y)));

    private static int CountShownPixels(MainWindow window, IntRect area, Func<Rgba, bool> match)
    {
        var compositor = window.Editor.Compositor;
        return CountPixels(area, (x, y) => match(compositor.Pixels[y * compositor.Width + x]));
    }

    private static int CountPixels(IntRect area, Func<int, int, bool> match)
    {
        var count = 0;
        for (var y = area.Y; y < area.Bottom; y++)
        {
            for (var x = area.X; x < area.Right; x++)
            {
                count += match(x, y) ? 1 : 0;
            }
        }

        return count;
    }

    private static bool IsInk(Rgba pixel) => pixel != Rgba.White;

    [AvaloniaFact]
    public void TextToolComesFromTheRegistryWithItsOptions()
    {
        var window = ShowWindowWithTextTool();

        Assert.Contains(ToolRegistry.Tools, t => t.Id == "text");
        var options = Options(window);
        Assert.True(options.IsEffectivelyVisible);
        Assert.Equal(2, options.GetLogicalDescendants().OfType<ComboBox>().Count());
        Assert.Equal(
            [TextStrings.Bold, TextStrings.Italic, TextStrings.Underline, TextStrings.Strikethrough, TextStrings.FillBackground],
            options.GetLogicalDescendants().OfType<ToggleButton>().Select(AutomationProperties.GetName));
        Assert.All(options.GetLogicalDescendants().OfType<Control>().Where(c => c is Button or ComboBox), c =>
        {
            Assert.True(c.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)));
        });
        Assert.False(Box(window).IsVisible);
    }

    [AvaloniaFact]
    public void ClickingOpensAFocusedTextBox_TypingShowsTheText_AndClickingOutsideCommitsItAsOneUndoableStep()
    {
        var window = ShowWindowWithTextTool();

        ClickCanvas(window, 20.5, 30.5);

        var box = Box(window);
        Assert.True(box.IsVisible);
        Assert.True(box.IsFocused);
        Assert.Equal(Avalonia.Media.Colors.Transparent, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(box.Foreground).Color); // the overlay shows the text
        Assert.Equal(new Rect(20, 30, TextTool.DefaultWidth, Tool(window).TextBounds!.Value.Height), box.Bounds);

        Type(window, "Hello");

        Assert.Equal("Hello", Tool(window).Text);
        var area = new IntRect(20, 30, 60, 20);
        Assert.True(CountShownPixels(window, area, IsInk) > 20);    // shown
        Assert.Equal(0, CountLayerPixels(window, area, IsInk));      // but pending
        Assert.False(window.Editor.History.CanUndo);

        ClickCanvas(window, 300.5, 190.5);

        Assert.False(box.IsVisible);
        Assert.Null(window.Editor.Adornment);                         // the click opened no new box
        Assert.True(CountLayerPixels(window, area, IsInk) > 20);
        Assert.True(window.Editor.History.CanUndo);

        window.KeyPress(Key.Z, Command(window), PhysicalKey.Z, null);
        window.KeyRelease(Key.Z, Command(window), PhysicalKey.Z, null);

        Assert.Equal(0, CountLayerPixels(window, area, IsInk));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void TypingLettersDoesNotTriggerToolShortcuts_AndEnterStartsANewLine()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        var oneLine = Tool(window).TextBounds!.Value.Height;

        Type(window, "pep");
        Press(window, Key.Enter);
        Type(window, "e");

        Assert.IsType<TextTool>(window.Editor.ActiveTool);
        Assert.Equal("pep" + Environment.NewLine + "e", Tool(window).Text.ReplaceLineEndings());
        Assert.True(Tool(window).TextBounds!.Value.Height >= 2 * oneLine - 1); // the box grew to fit
        Assert.Equal(Tool(window).TextBounds!.Value.Height, Box(window).Bounds.Height);
        Assert.NotNull(window.Editor.Adornment);
    }

    [AvaloniaFact]
    public void EscDiscardsTheText()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        Type(window, "Hello");

        Press(window, Key.Escape);

        Assert.False(Box(window).IsVisible);
        Assert.Equal(0, CountShownPixels(window, new IntRect(20, 30, 60, 20), IsInk));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ShortcutsToggleBoldItalicAndUnderline_AndTheOptionsFollow()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        Type(window, "Hello");
        var regular = CountShownPixels(window, new IntRect(20, 30, 100, 20), IsInk);

        Press(window, Key.B, Command(window));
        Press(window, Key.I, Command(window));
        Press(window, Key.U, Command(window));

        var settings = Tool(window).Settings;
        Assert.True(settings.Bold && settings.Italic && settings.Underline);
        var options = Options(window);
        Assert.True(options.FindControl<ToggleButton>("BoldButton")!.IsChecked);
        Assert.True(options.FindControl<ToggleButton>("ItalicButton")!.IsChecked);
        Assert.True(options.FindControl<ToggleButton>("UnderlineButton")!.IsChecked);
        Assert.Equal("Hello", Tool(window).Text);
        Assert.True(CountShownPixels(window, new IntRect(20, 30, 100, 20), IsInk) > regular); // re-rendered heavier
        Assert.Equal(Avalonia.Media.FontWeight.Bold, Box(window).FontWeight);                  // caret follows

        Press(window, Key.B, Command(window));
        Assert.False(settings.Bold);
        Assert.False(options.FindControl<ToggleButton>("BoldButton")!.IsChecked);
    }

    [AvaloniaFact]
    public void OptionsAndColoursApplyToThePendingText_AndFocusReturnsToTheBox()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        Type(window, "Hi");
        var options = Options(window);

        var fill = options.FindControl<ToggleButton>("FillBackgroundButton")!;
        fill.IsChecked = true;
        fill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Editor.PrimaryColor = Red;
        window.Editor.SecondaryColor = Blue;

        Assert.True(Tool(window).Settings.FillBackground);
        Assert.True(Box(window).IsFocused);
        var bounds = Tool(window).TextBounds!.Value;
        var shown = window.Editor.Compositor.Pixels;
        Assert.Equal(Blue, shown[(bounds.Bottom - 1) * window.Editor.Compositor.Width + bounds.Right - 1]); // background corner
        Assert.True(CountShownPixels(window, bounds, p => p.R > 200 && p.B < 50) > 10);                     // red text

        var sizeBox = options.FindControl<ComboBox>("SizeBox")!;
        sizeBox.SelectedItem = "36";
        Assert.Equal(36, Tool(window).Settings.Size);
        Assert.True(Tool(window).TextBounds!.Value.Height > bounds.Height);
        Assert.Equal(36 * TextRenderer.PixelsPerPoint, Box(window).FontSize);

        sizeBox.Text = "13.5";
        sizeBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(13.5, Tool(window).Settings.Size);
        sizeBox.Text = "huge";
        sizeBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(13.5, Tool(window).Settings.Size);
        Assert.Equal("13.5", sizeBox.Text);

        var fontBox = options.FindControl<ComboBox>("FontBox")!;
        Assert.Equal("fonts:Inter#Inter", fontBox.SelectedItem);
        var other = fontBox.Items.OfType<string>().First(name => name != "fonts:Inter#Inter"); // a system font
        fontBox.SelectedItem = other;
        Assert.Equal(other, Tool(window).Settings.FontFamily);
        Assert.Equal(other, Box(window).FontFamily.Name);
    }

    [AvaloniaFact]
    public void TheBoxLaysTextOutExactlyAsItIsRendered_SoTheCaretLinesUpWithTheGlyphs()
    {
        var window = ShowWindowWithTextTool();
        var settings = Tool(window).Settings;
        settings.Bold = true;
        settings.Size = 14;
        Drag(window, (20.5, 30.5), (120.5, 40.5));
        Type(window, "Wrapped text with several words");

        var box = Box(window);
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
        var bounds = Tool(window).TextBounds!.Value;
        var origin = presenter.TranslatePoint(default, window.FindControl<DocumentCanvas>("DocumentView")!)!.Value;
        Assert.Equal(new Point(bounds.X, bounds.Y), origin);

        using var rendered = TextRenderer.Layout(Tool(window).Text, settings.ToStyle(), bounds.Width, null);
        Assert.True(rendered.TextLines.Count > 1);
        Assert.Equal(rendered.TextLines.Count, presenter.TextLayout.TextLines.Count);
        for (var i = 0; i <= Tool(window).Text.Length; i++)
        {
            var expected = rendered.HitTestTextPosition(i);
            var actual = presenter.TextLayout.HitTestTextPosition(i);
            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Y, actual.Y, 3);
        }
    }

    [AvaloniaFact]
    public void DraggingTheFrameMovesTheBox_AndTheEditorFollows()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        Type(window, "Hi");
        var frame = window.Editor.Adornment!.Value.Frame;

        Drag(window, (frame.X + 60.5, frame.Y + 1.5), (frame.X + 100.5, frame.Y + 51.5)); // the frame's top edge

        Assert.Equal(new IntRect(60, 80, TextTool.DefaultWidth, Tool(window).TextBounds!.Value.Height), Tool(window).TextBounds);
        Assert.Equal(new Point(60, 80), Box(window).Bounds.Position);
        Assert.Equal("Hi", Box(window).Text);
    }

    [AvaloniaFact]
    public void SwitchingToolsCommitsTheText()
    {
        var window = ShowWindowWithTextTool();
        ClickCanvas(window, 20.5, 30.5);
        Type(window, "Hi");

        Click(window, ToolStrings.Pencil);

        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
        Assert.False(Box(window).IsVisible);
        Assert.True(CountLayerPixels(window, new IntRect(20, 30, 40, 20), IsInk) > 5);
    }
}
