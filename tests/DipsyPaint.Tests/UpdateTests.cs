using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using DipsyPaint.Engine;
using DipsyPaint.Updates;

namespace DipsyPaint.Tests;

public sealed class UpdateTests
{
    private sealed class FakeUpdater(Func<Task<bool>> download) : IUpdater
    {
        public int Applied { get; private set; }

        public Task<bool> DownloadUpdateAsync() => download();

        public void ApplyAfterExitAndRestart() => Applied++;
    }

    private static (MainWindow Window, UpdateControl Updates) ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        return (window, window.FindControl<UpdateControl>("Updates")!);
    }

    [AvaloniaFact]
    public async Task RestartButtonAppearsOnceAnUpdateIsDownloaded()
    {
        var (_, updates) = ShowWindow();
        Assert.False(updates.IsVisible);

        await updates.CheckAsync(new FakeUpdater(() => Task.FromResult(true)));

        Assert.True(updates.IsVisible);
        var button = updates.FindControl<Button>("RestartButton")!;
        Assert.Equal("Restart to update", button.Content);
        Assert.Equal("Restart to update", Avalonia.Automation.AutomationProperties.GetName(button));
    }

    [AvaloniaTheory]
    [InlineData("none")]
    [InlineData("offline")]
    [InlineData("faulted")]
    public async Task NoUpdateOrAFailedCheckStaysSilent(string outcome)
    {
        var (_, updates) = ShowWindow();
        var updater = new FakeUpdater(outcome switch
        {
            "none" => () => Task.FromResult(false),
            "offline" => () => throw new IOException("No such host is known."),
            _ => () => Task.FromException<bool>(new InvalidOperationException("rate limited")),
        });

        await updates.CheckAsync(updater); // does not throw

        Assert.False(updates.IsVisible);
        await updates.RestartToUpdateAsync();
        Assert.Equal(0, updater.Applied);
    }

    [AvaloniaFact]
    public async Task CheckRunsOffTheUiThread()
    {
        var (_, updates) = ShowWindow();
        bool? onUiThread = null;

        await updates.CheckAsync(new FakeUpdater(() =>
        {
            onUiThread = Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
            return Task.FromResult(false);
        }));

        Assert.False(onUiThread);
    }

    [AvaloniaFact]
    public async Task RestartClosesTheWindowThenApplies()
    {
        var (window, updates) = ShowWindow();
        var updater = new FakeUpdater(() => Task.FromResult(true));
        await updates.CheckAsync(updater);

        updates.FindControl<Button>("RestartButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(window.IsVisible);
        Assert.Equal(1, updater.Applied);
    }

    [AvaloniaFact]
    public async Task RestartDoesNotApplyWhenClosingIsCancelled()
    {
        var (window, updates) = ShowWindow();
        var updater = new FakeUpdater(() => Task.FromResult(true));
        await updates.CheckAsync(updater);
        window.Closing += (_, e) => e.Cancel = true; // e.g. the user keeps unsaved work

        await updates.RestartToUpdateAsync();

        Assert.True(window.IsVisible);
        Assert.Equal(0, updater.Applied);
    }

    [Fact]
    public void ACopyNotInstalledByVelopackHasNoUpdater()
    {
        // The test host, like a build or publish folder, is not a Velopack install: nothing to check.
        Assert.Null(VelopackUpdater.CreateIfInstalled());
    }
}
