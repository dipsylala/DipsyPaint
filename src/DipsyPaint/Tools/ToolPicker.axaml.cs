using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// Command-bar group with one toggle button per registered tool. Selecting a tool sets the
/// active tool of the <see cref="DocumentEditor"/> in the DataContext.
/// </summary>
public partial class ToolPicker : UserControl
{
    private readonly Dictionary<ToolDescriptor, ToggleButton> _buttons = [];

    public ToolPicker()
    {
        InitializeComponent();
        foreach (var tool in ToolRegistry.Tools)
        {
            var button = new ToggleButton
            {
                Content = new PathIcon { Data = Geometry.Parse(tool.IconData) },
                HotKey = tool.Shortcut,
                Width = 40,
                Height = 40,
            };
            AutomationProperties.SetName(button, tool.DisplayName);
            ToolTip.SetTip(button, tool.Shortcut is null
                ? tool.DisplayName
                : string.Format(ToolStrings.ToolTipFormat, tool.DisplayName, tool.Shortcut));
            button.Click += (_, _) => Select(tool);
            ToolButtons.Children.Add(button);
            _buttons[tool] = button;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ToolRegistry.Tools.Count > 0)
        {
            Select(ToolRegistry.Tools[0]);
        }
    }

    private void Select(ToolDescriptor tool)
    {
        foreach (var (descriptor, button) in _buttons)
        {
            button.IsChecked = ReferenceEquals(descriptor, tool);
        }

        if (DataContext is DocumentEditor editor)
        {
            editor.ActiveTool = tool.CreateTool();
        }
    }
}
