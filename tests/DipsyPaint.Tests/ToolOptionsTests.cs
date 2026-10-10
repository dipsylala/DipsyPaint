using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

public class ToolOptionsTests
{
    private const string Icon = "M0 0h24v24H0z";

    private static readonly ToolDescriptor WithOptions =
        new("with", "With options", Icon, null, () => new PencilTool(), () => new TextBlock { Text = "size" });

    private static readonly ToolDescriptor WithoutOptions =
        new("without", "Without options", Icon, null, () => new PencilTool());

    private static (ToolPicker Picker, ContentControl Host) ShowPicker()
    {
        var picker = new ToolPicker([WithOptions, WithoutOptions]);
        var window = new Window { Content = picker, DataContext = new DocumentEditor(Document.CreateBlank(8, 8)) };
        window.Show();
        return (picker, picker.FindControl<ContentControl>("ToolOptions")!);
    }

    private static void Click(ToolPicker picker, ToolDescriptor tool) =>
        picker.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == tool.DisplayName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void ShowsTheActiveToolsOptions()
    {
        var (_, host) = ShowPicker();

        Assert.Equal("size", Assert.IsType<TextBlock>(host.Content).Text);
    }

    [AvaloniaFact]
    public void ShowsNothingForAToolWithoutOptions()
    {
        var (picker, host) = ShowPicker();

        Click(picker, WithoutOptions);

        Assert.Null(host.Content);
    }

    [AvaloniaFact]
    public void ReusesTheOptionsPanelWhenTheToolIsReselected()
    {
        var (picker, host) = ShowPicker();
        var first = host.Content;

        Click(picker, WithoutOptions);
        Click(picker, WithOptions);

        Assert.Same(first, host.Content);
    }
}
