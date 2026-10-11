using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using DipsyPaint.Engine;
using DipsyPaint.Files;
using DipsyPaint.Layout;
using DipsyPaint.Resources;

namespace DipsyPaint.Layers;

/// <summary>
/// Reference Paint's layers panel for the <see cref="DocumentEditor"/> in the DataContext: one
/// thumbnail per layer, top layer first. Clicking a thumbnail selects the layer; its eye shows or
/// hides it; the Add button adds a layer above the selected one; dragging a thumbnail onto another
/// moves the layer there. Right-click (or Shift+F10) opens the layer's menu: hide or show,
/// duplicate, merge down, delete, and move up or down, which also make reordering possible without a
/// mouse. A command group docked right by default; on a top or bottom edge the thumbnails run
/// left to right.
/// </summary>
public partial class LayersPanel : UserControl
{
    private const double DragThreshold = 4;

    private readonly Dictionary<Layer, LayerRow> _rows = [];
    private DocumentEditor? _editor;
    private LayerRow? _pressedRow;
    private Point _pressedAt;
    private bool _dragging;

    public LayersPanel()
    {
        InitializeComponent();
        Dialogs = new WindowLayerDialogs(this);
        ApplyOrientation(CommandGroup.GetOrientation(this));
    }

    /// <summary>The dialogs to use; tests replace these to bypass real windows.</summary>
    public ILayerDialogs Dialogs { get; set; }

    /// <summary>The memory budget new and duplicated layers are checked against; null for <see cref="MemoryBudget.Default"/>.</summary>
    public MemoryBudget? Budget { get; set; }

    /// <summary>The rows, top layer first.</summary>
    public IEnumerable<LayerRow> LayerRows => Rows.Children.OfType<LayerRow>();

    /// <summary>The row showing <paramref name="layer"/>.</summary>
    public LayerRow RowOf(Layer layer) => _rows[layer];

    /// <summary>Adds a layer above the selected one, or reports that there isn't enough memory.</summary>
    public async Task AddLayerAsync()
    {
        if (_editor is { } editor && !editor.AddLayer(Budget))
        {
            await ShowNotEnoughMemoryAsync(editor);
        }
    }

    /// <summary>Duplicates <paramref name="layer"/>, or reports that there isn't enough memory.</summary>
    public async Task DuplicateLayerAsync(Layer layer)
    {
        if (_editor is { } editor && !editor.DuplicateLayer(layer, Budget))
        {
            await ShowNotEnoughMemoryAsync(editor);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_editor is not null)
        {
            _editor.LayersChanged -= OnDocumentChanged;
            _editor.StrokeEnded -= OnDocumentChanged;
            _editor.SizeChanged -= OnDocumentChanged;
            _editor.History.Changed -= OnDocumentChanged;
        }

        _editor = DataContext as DocumentEditor;
        if (_editor is not null)
        {
            _editor.LayersChanged += OnDocumentChanged;
            _editor.StrokeEnded += OnDocumentChanged;
            _editor.SizeChanged += OnDocumentChanged;
            // Undo and redo, which may change the layers, and every recorded step (e.g. a commit).
            _editor.History.Changed += OnDocumentChanged;
        }

        _rows.Clear();
        Rows.Children.Clear();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandGroup.OrientationProperty)
        {
            ApplyOrientation(change.GetNewValue<Orientation>());
        }
    }

    private void ApplyOrientation(Orientation orientation)
    {
        var vertical = orientation == Orientation.Vertical;
        DockPanel.SetDock(AddButton, vertical ? Dock.Top : Dock.Left);
        Scroller.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        Scroller.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => Refresh();

    // Brings the rows in line with the document: one per layer, top first, with current thumbnails.
    private void Refresh()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var layers = editor.Document.Layers;
        foreach (var gone in _rows.Keys.Except(layers).ToList())
        {
            _rows.Remove(gone);
        }

        var ordered = layers.Reverse().Select(l => _rows.TryGetValue(l, out var row) ? row : _rows[l] = CreateRow(l)).ToList();
        if (!Rows.Children.SequenceEqual(ordered))
        {
            Rows.Children.Clear();
            Rows.Children.AddRange(ordered);
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Update(layers.Count - i, ordered[i].Layer == editor.Document.ActiveLayer);
        }
    }

    private LayerRow CreateRow(Layer layer)
    {
        var row = new LayerRow(layer);
        row.Select.Click += (_, _) =>
        {
            _editor?.SelectLayer(layer);
            Refresh(); // a click on the selected layer unchecked it
        };
        row.Visibility.Click += (_, _) => _editor?.SetLayerVisible(layer, row.Visibility.IsChecked == true);
        row.Select.ContextFlyout = CreateMenu(layer);
        // Avalonia opens the menu with the Menu key; Shift+F10 is the other usual key for it.
        row.Select.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift)
            {
                row.Select.ContextFlyout?.ShowAt(row.Select);
                e.Handled = true;
            }
        };
        row.Select.AddHandler(PointerPressedEvent, (_, e) => OnRowPressed(row, e), RoutingStrategies.Tunnel);
        row.Select.AddHandler(PointerMovedEvent, (_, e) => OnRowMoved(e), RoutingStrategies.Tunnel);
        // After the button's own handling, so it has released the pointer.
        row.Select.AddHandler(PointerReleasedEvent, (_, e) => OnRowReleased(e), RoutingStrategies.Bubble, handledEventsToo: true);
        row.Select.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), RoutingStrategies.Direct);
        return row;
    }

    private MenuFlyout CreateMenu(Layer layer)
    {
        var menu = new MenuFlyout();
        var visibility = CreateItem(LayerStrings.HideLayer, () => _editor?.SetLayerVisible(layer, !layer.IsVisible));
        var duplicate = CreateItem(LayerStrings.DuplicateLayer, async () => await DuplicateLayerAsync(layer));
        var merge = CreateItem(LayerStrings.MergeDown, () => _editor?.MergeDown(layer));
        var delete = CreateItem(LayerStrings.DeleteLayer, () => _editor?.DeleteLayer(layer));
        var up = CreateItem(LayerStrings.MoveLayerUp, () => Move(layer, 1));
        var down = CreateItem(LayerStrings.MoveLayerDown, () => Move(layer, -1));
        menu.Items.Add(visibility);
        menu.Items.Add(duplicate);
        menu.Items.Add(merge);
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        menu.Items.Add(up);
        menu.Items.Add(down);
        menu.Opening += (_, _) =>
        {
            if (_editor is not { } editor)
            {
                return;
            }

            var text = layer.IsVisible ? LayerStrings.HideLayer : LayerStrings.ShowLayer;
            visibility.Header = text;
            AutomationProperties.SetName(visibility, text);
            merge.IsEnabled = editor.CanMergeDown(layer);
            delete.IsEnabled = editor.CanDeleteLayer;
            var index = IndexOf(layer);
            up.IsEnabled = index < editor.Document.Layers.Count - 1;
            down.IsEnabled = index > 0;
        };
        return menu;
    }

    private MenuItem CreateItem(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        AutomationProperties.SetName(item, text);
        item.Click += (_, _) =>
        {
            action();
            // The menu's row may be gone or moved; keep the keyboard on the selected layer.
            if (_editor is { } editor && _rows.TryGetValue(editor.Document.ActiveLayer, out var row))
            {
                row.Select.Focus(NavigationMethod.Tab);
            }
        };
        return item;
    }

    private void Move(Layer layer, int by) => _editor?.MoveLayer(layer, IndexOf(layer) + by);

    private int IndexOf(Layer layer) => _editor is { } editor ? editor.Document.Layers.ToList().IndexOf(layer) : -1;

    private void OnRowPressed(LayerRow row, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(row.Select).Properties.IsLeftButtonPressed)
        {
            _pressedRow = row;
            _pressedAt = e.GetPosition(Rows);
            _dragging = false;
        }
    }

    private void OnRowMoved(PointerEventArgs e)
    {
        if (_pressedRow is { } row && !_dragging)
        {
            var delta = e.GetPosition(Rows) - _pressedAt;
            _dragging = Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold;
            row.Opacity = _dragging ? 0.5 : 1; // shows which layer is being moved
        }
    }

    // A drop on another row moves the dragged layer to that row's place in the stack.
    private void OnRowReleased(PointerReleasedEventArgs e)
    {
        var (row, dragged) = (_pressedRow, _dragging);
        EndDrag();
        if (row is null || !dragged || _editor is not { } editor)
        {
            return;
        }

        var position = e.GetPosition(Rows);
        if (LayerRows.FirstOrDefault(r => r.Bounds.Contains(position)) is { } target && target != row)
        {
            editor.MoveLayer(row.Layer, IndexOf(target.Layer));
        }
    }

    private void EndDrag()
    {
        _pressedRow?.Opacity = 1;
        _pressedRow = null;
        _dragging = false;
    }

    private Task ShowNotEnoughMemoryAsync(DocumentEditor editor) =>
        Dialogs.ShowErrorAsync(string.Format(LayerStrings.NotEnoughMemory, editor.Document.Width, editor.Document.Height));

    private async void OnAddClick(object? sender, RoutedEventArgs e) => await AddLayerAsync();
}

/// <summary>The dialogs behind the layer commands. Tests substitute their own to bypass real windows.</summary>
public interface ILayerDialogs
{
    Task ShowErrorAsync(string message);
}

/// <summary>Shows the File menu's message box, modal to the window.</summary>
internal sealed class WindowLayerDialogs(Visual anchor) : ILayerDialogs
{
    public Task ShowErrorAsync(string message) => new StorageProviderDialogs(anchor).ShowErrorAsync(message);
}
