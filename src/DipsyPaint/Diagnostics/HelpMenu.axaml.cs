using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DipsyPaint.Diagnostics;

/// <summary>
/// Help menu with "Report a problem", standing in for the help and feedback entries in reference
/// Paint's Settings. Reporting only hands a pre-filled GitHub issue URL to the browser (ADR-0017).
/// </summary>
public partial class HelpMenu : UserControl
{
    public HelpMenu() => InitializeComponent();

    /// <summary>Opens a URL in the browser; tests replace it. Null means the window's launcher.</summary>
    public Func<Uri, Task<bool>>? Launch { get; set; }

    /// <summary>Where the newest crash log is read from; tests replace it.</summary>
    public CrashLog CrashLog { get; set; } = CrashHandler.Installed?.Log ?? new CrashLog(CrashLog.DefaultDirectory);

    /// <summary>Opens the pre-filled issue; false if the browser could not be opened.</summary>
    public Task<bool> ReportProblemAsync()
    {
        var url = ProblemReport.BuildUrl(AppInfo.Version, AppInfo.Os, AppInfo.Runtime, CrashLog.ReadNewest());
        return (Launch ?? LaunchWithTopLevel)(url);
    }

    private Task<bool> LaunchWithTopLevel(Uri url) =>
        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(url) ?? Task.FromResult(false);

    private async void OnReportProblemClick(object? sender, RoutedEventArgs e) => await ReportProblemAsync();
}
