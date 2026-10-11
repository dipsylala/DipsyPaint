using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DipsyPaint.View;

// Full screen, as in reference Paint: F11 shows the picture alone, filling the screen, with the
// command bar, status bar, rulers and gridlines hidden. F11, Esc or a click returns to the window
// as it was. Full screen is not remembered between sessions.
public partial class ViewMenu
{
    private Window? _window;
    private WindowState _stateBeforeFullScreen = WindowState.Normal;

    /// <summary>Whether the picture is shown full screen.</summary>
    public bool IsFullScreen { get; private set; }

    /// <summary>Enters or leaves full screen.</summary>
    public void SetFullScreen(bool fullScreen)
    {
        if (IsFullScreen == fullScreen)
        {
            return;
        }

        IsFullScreen = fullScreen;
        Apply();
        if (_window is null)
        {
            return;
        }

        if (fullScreen)
        {
            _stateBeforeFullScreen = _window.WindowState;
            _window.WindowState = WindowState.FullScreen;
        }
        else if (_window.WindowState == WindowState.FullScreen)
        {
            _window.WindowState = _stateBeforeFullScreen;
        }
    }

    private void AttachFullScreen()
    {
        _window = _topLevel as Window;
        // Tunnel, so in full screen these keys and clicks only leave it (Esc would otherwise cancel a pending selection).
        _topLevel?.AddHandler(KeyDownEvent, OnFullScreenKeyDown, RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(PointerPressedEvent, OnFullScreenPointerPressed, RoutingStrategies.Tunnel);
        _window?.PropertyChanged += OnWindowPropertyChanged;
    }

    private void DetachFullScreen()
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnFullScreenKeyDown);
        _topLevel?.RemoveHandler(PointerPressedEvent, OnFullScreenPointerPressed);
        _window?.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
    }

    private void OnFullScreenKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && (e.Key == Key.F11 || (IsFullScreen && e.Key == Key.Escape)))
        {
            SetFullScreen(!IsFullScreen);
            e.Handled = true;
        }
    }

    private void OnFullScreenPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFullScreen)
        {
            SetFullScreen(false);
            e.Handled = true;
        }
    }

    // The window can leave full screen without us, e.g. through macOS's window controls; show the chrome again.
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && IsFullScreen && _window?.WindowState != WindowState.FullScreen)
        {
            IsFullScreen = false;
            Apply();
        }
    }
}
