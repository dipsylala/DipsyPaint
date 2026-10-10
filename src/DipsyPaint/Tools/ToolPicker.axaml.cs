using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// Command-bar group with one toggle button per registered tool, followed by the active tool's
/// options panel. Selecting a tool sets the active tool of the <see cref="DocumentEditor"/> in
/// the DataContext.
/// </summary>
public partial class ToolPicker : UserControl
{
    private readonly IReadOnlyList<ToolDescriptor> _tools;
    private readonly Dictionary<ToolDescriptor, ToggleButton> _buttons = [];
    private readonly Dictionary<ToolDescriptor, Control> _options = [];

    public ToolPicker() : this(ToolRegistry.Tools)
    {
    }

    public ToolPicker(IReadOnlyList<ToolDescriptor> tools)
    {
        _tools = tools;
        InitializeComponent();
        foreach (var tool in tools)
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
        if (_tools.Count > 0)
        {
            Select(_tools[0]);
        }
    }

    private void Select(ToolDescriptor tool)
    {
        foreach (var (descriptor, button) in _buttons)
        {
            button.IsChecked = ReferenceEquals(descriptor, tool);
        }

        if (tool.CreateOptions is not null && !_options.ContainsKey(tool))
        {
            _options[tool] = tool.CreateOptions();
        }

        ToolOptions.Content = _options.GetValueOrDefault(tool);

        if (DataContext is DocumentEditor editor)
        {
            editor.ActiveTool = tool.CreateTool();
        }
    }
}
