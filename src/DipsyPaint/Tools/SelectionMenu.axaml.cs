using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Tools;

/// <summary>
/// Reference Paint's Selection menu (in the Image group): rectangle or free-form selection (plus our
/// magic wand, ADR-0011), select
/// all, invert selection, delete and transparent selection, for the <see cref="DocumentEditor"/> in
/// the DataContext. Picking a shape (or selecting all, or inverting) switches to the selection tool.
/// The options are <see cref="ToolRegistry.SelectionSettings"/>, which this menu also gives the editor
/// (<see cref="DocumentEditor.SelectionSettings"/>). Also handles Paint's shortcuts for the whole
/// window: Ctrl+A (select all) and Ctrl+Shift+I (invert selection), Cmd on macOS; Delete is the
/// canvas's.
/// </summary>
public partial class SelectionMenu : UserControl
{
    private readonly SelectionSettings _settings;
    private DocumentEditor? _editor;
    private TopLevel? _topLevel;

    public SelectionMenu() : this(ToolRegistry.SelectionSettings)
    {
    }

    public SelectionMenu(SelectionSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        UpdateItems();
    }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Makes the next selection <paramref name="shape"/>, switching to the selection tool.</summary>
    public void UseShape(SelectionShape shape)
    {
        _settings.Shape = shape;
        if (_editor is { ActiveTool: not SelectionTool } editor)
        {
            editor.ActiveTool = new SelectionTool(_settings);
        }

        UpdateItems();
    }

    /// <summary>Turns transparent selection on or off; a lifted selection shows the change at once.</summary>
    public void SetTransparent(bool transparent)
    {
        _settings.Transparent = transparent;
        _editor?.RefreshPending();
        UpdateItems();
    }

    public void SelectAll() => _editor?.SelectAll();

    public void InvertSelection() => _editor?.InvertSelection();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.SelectionChanged -= OnSelectionChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.SelectionSettings = _settings;
        _editor?.SelectionChanged += OnSelectionChanged;
        UpdateItems();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
        SelectAllItem.InputGesture = new KeyGesture(Key.A, CommandModifiers);
        InvertItem.InputGesture = new KeyGesture(Key.I, CommandModifiers | KeyModifiers.Shift);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Bubbling, like the other window shortcuts, so a focused text box keeps Ctrl+A for its text.
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        var command = CommandModifiers;
        if (e.Key == Key.A && e.KeyModifiers == command)
        {
            e.Handled = true;
            SelectAll();
        }
        else if (e.Key == Key.I && e.KeyModifiers == (command | KeyModifiers.Shift))
        {
            e.Handled = true;
            InvertSelection();
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateItems();

    private void UpdateItems()
    {
        RectangleItem.IsChecked = _settings.Shape == SelectionShape.Rectangle;
        FreeFormItem.IsChecked = _settings.Shape == SelectionShape.FreeForm;
        MagicWandItem.IsChecked = _settings.Shape == SelectionShape.MagicWand;
        TransparentItem.IsChecked = _settings.Transparent;
        DeleteItem.IsEnabled = _editor?.Selection is not null;
    }

    private void OnRectangleClick(object? sender, RoutedEventArgs e) => UseShape(SelectionShape.Rectangle);

    private void OnFreeFormClick(object? sender, RoutedEventArgs e) => UseShape(SelectionShape.FreeForm);

    private void OnMagicWandClick(object? sender, RoutedEventArgs e) => UseShape(SelectionShape.MagicWand);

    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => SelectAll();

    private void OnInvertClick(object? sender, RoutedEventArgs e) => InvertSelection();

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => _editor?.DeleteSelection();

    private void OnTransparentClick(object? sender, RoutedEventArgs e) => SetTransparent(!_settings.Transparent);
}
