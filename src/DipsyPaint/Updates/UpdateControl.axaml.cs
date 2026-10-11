using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DipsyPaint.Updates;

/// <summary>
/// "Restart to update": hidden until a newer release has been downloaded in the background. Ignoring
/// it is fine: Velopack installs a downloaded release the next time the app starts
/// (<c>VelopackApp.Run</c> in <c>Main</c>).
/// </summary>
public partial class UpdateControl : UserControl
{
    private IUpdater? _updater;

    public UpdateControl() => InitializeComponent();

    /// <summary>
    /// Checks for and downloads an update off the UI thread, then shows the button if one is ready.
    /// Never throws and never blocks: offline, rate-limited or any other failure keeps it hidden.
    /// </summary>
    public async Task CheckAsync(IUpdater updater)
    {
        try
        {
            if (await Task.Run(updater.DownloadUpdateAsync))
            {
                _updater = updater;
                IsVisible = true;
            }
        }
        catch
        {
            // Best effort (ADR-0017): a failed check says nothing and is retried at the next start.
        }
    }

    /// <summary>
    /// Closes the window, after the "Save changes?" prompt if there are unsaved changes, and, only if
    /// it really closes, has Velopack apply the update and restart the app.
    /// </summary>
    public async Task RestartToUpdateAsync()
    {
        if (_updater is not { } updater || TopLevel.GetTopLevel(this) is not MainWindow window)
        {
            return;
        }

        if (await window.FileMenu.CloseWindowAsync())
        {
            updater.ApplyAfterExitAndRestart();
        }
    }

    private async void OnRestartClick(object? sender, RoutedEventArgs e) => await RestartToUpdateAsync();
}
