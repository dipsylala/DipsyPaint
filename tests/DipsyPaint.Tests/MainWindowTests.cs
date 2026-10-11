using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using DipsyPaint.Engine;

namespace DipsyPaint.Tests;

public class MainWindowTests
{
    private static MainWindow ShowWindow(int width = 640, int height = 480)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void ShowsBlankWhiteDocumentAtDocumentSize()
    {
        var window = ShowWindow(640, 480);

        var canvas = window.FindControl<Border>("Canvas")!;
        Assert.True(canvas.IsEffectivelyVisible);
        Assert.Equal(new Size(640, 480), canvas.Bounds.Size);
        Assert.Equal(Colors.White, ((ISolidColorBrush)canvas.Background!).Color);
    }

    [AvaloniaFact]
    public void StatusBarShowsCanvasSize()
    {
        var window = ShowWindow(640, 480);

        Assert.Equal("640 × 480 px · 96 dpi", window.FindControl<TextBlock>("CanvasSizeText")!.Text);
    }

    [AvaloniaFact]
    public void UsesDipsyPaintIdentity()
    {
        var window = ShowWindow();

        Assert.Equal("Untitled - DipsyPaint", window.Title);
        Assert.NotNull(window.Icon);
        var texts = window.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Append(window.Title!);
        Assert.DoesNotContain(texts, t => t.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                                          || t.Contains("MS Paint", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void ThemeFollowsOperatingSystem()
    {
        Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void HasNoPrintCommand()
    {
        var window = ShowWindow();

        var commandLabels = window.GetLogicalDescendants().Select(c => c switch
        {
            MenuItem m => m.Header?.ToString(),
            Button b => b.Content?.ToString(),
            _ => null,
        });
        Assert.DoesNotContain(commandLabels, l => l?.Contains("Print", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(window.KeyBindings, k => k.Gesture == new KeyGesture(Key.P, KeyModifiers.Control));
    }

    [AvaloniaFact]
    public void CommandsAreKeyboardReachableAndNamed()
    {
        var window = ShowWindow();

        var commands = window.GetLogicalDescendants().OfType<Control>().Where(c => c is Button or MenuItem);
        Assert.All(commands, c =>
        {
            Assert.True(c.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)));
        });
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(window.FindControl<Border>("Canvas")!)));
    }
}
