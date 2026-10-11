using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// Command-bar group with one toggle button per registered tool, followed by the active tool's
/// options panel. Selecting a tool sets the active tool of the <see cref="DocumentEditor"/> in
/// the DataContext; when the editor switches tools itself (e.g. Paste selects the selection tool),
/// the matching button is checked.
/// </summary>
public partial class ToolPicker : UserControl
{
    private readonly IReadOnlyList<ToolDescriptor> _tools;
    private readonly Dictionary<ToolDescriptor, ToggleButton> _buttons = [];
    private readonly Dictionary<ToolDescriptor, Control> _options = [];
    private DocumentEditor? _editor;
    private ToolDescriptor? _active;
    private ToolDescriptor? _previous;
    private TopLevel? _topLevel;

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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Tool shortcuts are plain letters, so they must not fire while the user types into a text box
    // (e.g. the text tool's): bubbling, and ignored when a text box has the keyboard focus.
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || _tools.FirstOrDefault(t => t.Shortcut?.Matches(e) == true) is not { } tool)
        {
            return;
        }

        Select(tool);
        e.Handled = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.StrokeEnded -= OnStrokeEnded;
        _editor?.ActiveToolChanged -= OnActiveToolChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.StrokeEnded += OnStrokeEnded;
        _editor?.ActiveToolChanged += OnActiveToolChanged;
        // A new editor (e.g. after File > New) keeps the tool the user had selected.
        if (_tools.Count > 0)
        {
            Select(_active ?? _tools[0]);
        }
    }

    private void OnStrokeEnded(object? sender, EventArgs e)
    {
        if (_active?.ReturnsToPreviousTool == true && _previous is not null)
        {
            Select(_previous);
        }
    }

    // Shows a tool the editor switched to without the command bar, if it is a registered one.
    private void OnActiveToolChanged(object? sender, EventArgs e)
    {
        var type = _editor?.ActiveTool?.GetType();
        if (type is not null && _active?.CreateTool().GetType() != type
            && _tools.FirstOrDefault(t => t.CreateTool().GetType() == type) is { } tool)
        {
            Show(tool);
        }
    }

    private void Select(ToolDescriptor tool)
    {
        Show(tool);
        _editor?.ActiveTool = tool.CreateTool();
    }

    // Checks the tool's button and shows its options, without changing the editor's tool.
    private void Show(ToolDescriptor tool)
    {
        if (_active is not null && !ReferenceEquals(_active, tool))
        {
            _previous = _active;
        }

        _active = tool;
        foreach (var (descriptor, button) in _buttons)
        {
            button.IsChecked = ReferenceEquals(descriptor, tool);
        }

        if (tool.CreateOptions is not null && !_options.ContainsKey(tool))
        {
            _options[tool] = tool.CreateOptions();
        }

        ToolOptions.Content = _options.GetValueOrDefault(tool);
    }
}
