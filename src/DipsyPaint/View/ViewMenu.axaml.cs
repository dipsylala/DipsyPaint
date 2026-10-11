using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DipsyPaint.Settings;

namespace DipsyPaint.View;

/// <summary>
/// Reference Paint's View menu: zoom commands, and the display options rulers (Ctrl+R), gridlines
/// (Ctrl+G) and status bar. The display options only change what is shown around and over the
/// canvas; they never touch the document, so they are never saved into the image.
/// </summary>
public partial class ViewMenu : UserControl
{
    private ZoomControl? _zoom;
    private Control? _statusBar;
    private CanvasRulers? _rulers;
    private Gridlines? _gridlines;
    private TopLevel? _topLevel;

    public ViewMenu()
    {
        InitializeComponent();
        Apply();
    }

    /// <summary>Whether rulers show along the top and left of the canvas area. Off by default, as in reference Paint.</summary>
    public bool ShowRulers { get; private set; }

    /// <summary>Whether gridlines show over the canvas. Off by default, as in reference Paint.</summary>
    public bool ShowGridlines { get; private set; }

    /// <summary>Whether the status bar shows. On by default, as in reference Paint.</summary>
    public bool ShowStatusBar { get; private set; } = true;

    /// <summary>Raised when the user changes a display option, so it can be saved.</summary>
    public event EventHandler? Changed;

    /// <summary>Connects the menu to the parts of the window it zooms, shows and hides.</summary>
    public void Connect(ZoomControl zoom, Control statusBar, CanvasRulers rulers, Gridlines gridlines)
    {
        _zoom = zoom;
        _statusBar = statusBar;
        _rulers = rulers;
        _gridlines = gridlines;
        Apply();
    }

    /// <summary>Restores saved display options; missing values keep their defaults.</summary>
    public void Restore(ViewSettings? settings)
    {
        ShowRulers = settings?.Rulers ?? false;
        ShowGridlines = settings?.Gridlines ?? false;
        ShowStatusBar = settings?.StatusBar ?? true;
        Apply();
    }

    /// <summary>The display options, for the settings file.</summary>
    public ViewSettings ToSettings() => new() { Rulers = ShowRulers, Gridlines = ShowGridlines, StatusBar = ShowStatusBar };

    public void SetRulers(bool show)
    {
        if (ShowRulers != show)
        {
            ShowRulers = show;
            OnChanged();
        }
    }

    public void SetGridlines(bool show)
    {
        if (ShowGridlines != show)
        {
            ShowGridlines = show;
            OnChanged();
        }
    }

    public void SetStatusBar(bool show)
    {
        if (ShowStatusBar != show)
        {
            ShowStatusBar = show;
            OnChanged();
        }
    }

    private void OnChanged()
    {
        Apply();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        RulersItem.IsChecked = ShowRulers;
        GridlinesItem.IsChecked = ShowGridlines;
        StatusBarItem.IsChecked = ShowStatusBar;
        _rulers?.IsShown = ShowRulers;
        _gridlines?.IsVisible = ShowGridlines;
        _statusBar?.IsVisible = ShowStatusBar;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
        RulersItem.InputGesture = new KeyGesture(Key.R, CommandModifiers);
        GridlinesItem.InputGesture = new KeyGesture(Key.G, CommandModifiers);
        ZoomInItem.InputGesture = new KeyGesture(Key.PageUp, CommandModifiers);
        ZoomOutItem.InputGesture = new KeyGesture(Key.PageDown, CommandModifiers);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    // Bubbling, like the other window shortcuts, so a focused text box can claim the keys first.
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != CommandModifiers)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.R:
                SetRulers(!ShowRulers);
                break;
            case Key.G:
                SetGridlines(!ShowGridlines);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => _zoom?.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => _zoom?.ZoomOut();

    private void OnActualSizeClick(object? sender, RoutedEventArgs e) => _zoom?.ZoomTo(1);

    private void OnFitToWindowClick(object? sender, RoutedEventArgs e) => _zoom?.FitToWindow();

    private void OnRulersClick(object? sender, RoutedEventArgs e) => SetRulers(!ShowRulers);

    private void OnGridlinesClick(object? sender, RoutedEventArgs e) => SetGridlines(!ShowGridlines);

    private void OnStatusBarClick(object? sender, RoutedEventArgs e) => SetStatusBar(!ShowStatusBar);
}
