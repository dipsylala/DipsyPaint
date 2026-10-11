using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DipsyPaint.Diagnostics;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Files;
using DipsyPaint.Recovery;

namespace DipsyPaint.Tests;

/// <summary>
/// Crash recovery in the window (ADR-0013). The recovery folder is a temporary one and the timers are
/// fake: nothing here touches the user's app data or waits two minutes.
/// </summary>
public sealed class CrashRecoveryTests : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    private readonly string _root = Directory.CreateTempSubdirectory("dipsypaint-recovery-").FullName;
    private readonly List<RecoveryStore> _stores = [];

    public CrashRecoveryTests()
    {
        Directory.CreateDirectory(RecoveryFolder);
        Directory.CreateDirectory(UserFolder);
    }

    private string RecoveryFolder => Path.Combine(_root, "Recovery");

    private string UserFolder => Path.Combine(_root, "Pictures");

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeTimer(TimerCallback callback, object? state, TimeSpan period) : ITimer
    {
        public TimeSpan Period { get; } = period;
        public bool IsDisposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTime : TimeProvider
    {
        public List<FakeTimer> Timers { get; } = [];

        public FakeTimer Running => Timers.Single(t => !t.IsDisposed);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(callback, state, period);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class FakePrompt(RecoveryChoice choice) : IRecoveryPrompt
    {
        public List<RecoverySnapshot> Asked { get; } = [];

        public Task<RecoveryChoice> AskAsync(RecoverySnapshot snapshot)
        {
            Asked.Add(snapshot);
            return Task.FromResult(choice);
        }
    }

    private sealed class FakeSaveChangesPrompt(SaveChangesChoice choice) : ISaveChangesPrompt
    {
        public int Asked { get; private set; }

        public Task<SaveChangesChoice> AskAsync(string documentName)
        {
            Asked++;
            return Task.FromResult(choice);
        }
    }

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }
        public List<string> Errors { get; } = [];

        public Task<(int Width, int Height, Resolution Resolution)?> AskNewImageSizeAsync(int width, int height) => Task.FromResult<(int, int, Resolution)?>((width, height, Resolution.Default));

        public Task<PickedFile?> PickOpenAsync() => Task.FromResult(OpenResult);

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format) => Task.FromResult(SaveResult);

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmLossySaveAsync() => Task.FromResult(true);
    }

    private sealed record Session(MainWindow Window, FileMenu Menu, FakeDialogs Dialogs, CrashRecovery Recovery, FakeTime Time);

    /// <summary>A launch of DipsyPaint: a window with crash recovery on a temporary recovery folder.</summary>
    private Session Launch(CrashHandler? crashes = null)
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        menu.SaveChangesPrompt = new FakeSaveChangesPrompt(SaveChangesChoice.DontSave);
        var store = new RecoveryStore(RecoveryFolder);
        _stores.Add(store);
        var time = new FakeTime();
        return new Session(window, menu, dialogs, new CrashRecovery(window, store, Interval, time, crashes), time);
    }

    private static void DrawDot(MainWindow window, int x, int y)
    {
        window.Editor.ActiveTool = new PencilTool();
        window.Editor.Apply(new PointerInput(PointerAction.Down, x + 0.5, y + 0.5, PointerButton.Left));
        window.Editor.Apply(new PointerInput(PointerAction.Up, x + 0.5, y + 0.5, PointerButton.Left));
    }

    /// <summary>The timer fires: the snapshot is taken on the UI thread and written in the background.</summary>
    private static async Task TimerFires(Session session)
    {
        session.Time.Running.Fire();
        Dispatcher.UIThread.RunJobs();
        await session.Recovery.PendingWrite;
    }

    private static Document OpenSnapshot(string path)
    {
        using var stream = File.OpenRead(path);
        return ImageCodec.Open(stream);
    }

    private PickedFile UserFile(string name, Document? content = null)
    {
        var path = Path.Combine(UserFolder, name);
        if (content is not null)
        {
            using var file = File.Create(path);
            ImageCodec.Export(new Compositor(content), file, ImageFormat.Png);
        }

        return new PickedFile(name, () => Task.FromResult<Stream>(File.OpenRead(path)), () => Task.FromResult<Stream>(File.Create(path)));
    }

    // A crash: the process dies without closing the window; the OS releases its file locks.
    private static void Crash(Session session) => session.Recovery.Store.Dispose();

    [AvaloniaFact]
    public async Task WhileThereAreUnsavedChanges_ASnapshotIsWrittenEveryInterval()
    {
        var session = Launch();
        Assert.False(session.Recovery.IsTakingSnapshots);
        Assert.Empty(session.Time.Timers);

        DrawDot(session.Window, 3, 4);

        Assert.True(session.Recovery.IsTakingSnapshots);
        Assert.Equal(Interval, session.Time.Running.Period);
        Assert.False(File.Exists(session.Recovery.Store.SnapshotPath));

        await TimerFires(session);
        var snapshot = OpenSnapshot(session.Recovery.Store.SnapshotPath);
        Assert.Equal(Rgba.Black, snapshot.ActiveLayer.Pixels.GetPixel(3, 4));

        DrawDot(session.Window, 5, 6);
        await TimerFires(session);
        Assert.Equal(Rgba.Black, OpenSnapshot(session.Recovery.Store.SnapshotPath).ActiveLayer.Pixels.GetPixel(5, 6));
    }

    [AvaloniaFact]
    public async Task ThePixelsAreCopiedWhenTheSnapshotIsTaken_SoLaterEditsDoNotReachIt()
    {
        var session = Launch();
        DrawDot(session.Window, 3, 4);

        session.Time.Running.Fire();
        Dispatcher.UIThread.RunJobs();
        DrawDot(session.Window, 5, 6); // while the write may still be running on its background thread
        await session.Recovery.PendingWrite;

        var snapshot = OpenSnapshot(session.Recovery.Store.SnapshotPath);
        Assert.Equal(Rgba.Black, snapshot.ActiveLayer.Pixels.GetPixel(3, 4));
        Assert.Equal(Rgba.White, snapshot.ActiveLayer.Pixels.GetPixel(5, 6));
    }

    [AvaloniaFact]
    public async Task UndoingBackToTheSavedState_StopsSnapshots_AndDeletesTheSnapshot()
    {
        var session = Launch();
        DrawDot(session.Window, 3, 4);
        await TimerFires(session);

        session.Window.Editor.Undo();

        Assert.False(session.Recovery.IsTakingSnapshots);
        Assert.All(session.Time.Timers, t => Assert.True(t.IsDisposed));
        Assert.False(File.Exists(session.Recovery.Store.SnapshotPath));
    }

    [AvaloniaFact]
    public async Task Snapshots_NeverWriteToTheUsersFile_AndSavingDeletesThem()
    {
        var session = Launch();
        session.Dialogs.OpenResult = UserFile("cat.png", Document.CreateBlank(64, 48));
        await session.Menu.OpenAsync();
        var userFile = Path.Combine(UserFolder, "cat.png");
        var original = File.ReadAllBytes(userFile);

        DrawDot(session.Window, 3, 4);
        await TimerFires(session);

        Assert.Equal(original, File.ReadAllBytes(userFile));
        Assert.Equal("cat.png", Path.GetFileName(Assert.Single(Directory.GetFiles(UserFolder))));
        Assert.StartsWith(RecoveryFolder, session.Recovery.Store.SnapshotPath);
        Assert.True(File.Exists(session.Recovery.Store.SnapshotPath));

        Assert.True(await session.Menu.SaveAsync());

        Assert.False(File.Exists(session.Recovery.Store.SnapshotPath));
        Assert.False(session.Recovery.IsTakingSnapshots);
        Assert.Equal(Rgba.Black, OpenSnapshot(userFile).ActiveLayer.Pixels.GetPixel(3, 4));
    }

    [AvaloniaFact]
    public async Task DiscardingTheChanges_WithNew_DeletesTheSnapshot()
    {
        var session = Launch();
        DrawDot(session.Window, 3, 4);
        await TimerFires(session);

        await session.Menu.NewAsync(); // "Save changes?" → Don't save

        Assert.False(File.Exists(session.Recovery.Store.SnapshotPath));
        Assert.False(session.Recovery.IsTakingSnapshots);
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_IsACleanExit_ThatLeavesNothingToRecover()
    {
        var session = Launch();
        DrawDot(session.Window, 3, 4);
        await TimerFires(session);

        Assert.True(await session.Menu.CloseWindowAsync()); // "Save changes?" → Don't save

        Assert.Empty(Directory.GetFiles(RecoveryFolder));
        var next = Launch();
        next.Recovery.Prompt = new FakePrompt(RecoveryChoice.Restore);
        await next.Recovery.OfferRestoreAsync();
        Assert.Empty(((FakePrompt)next.Recovery.Prompt).Asked);
    }

    [AvaloniaFact]
    public async Task AfterAnUncleanExit_TheNextLaunchOffersTheSnapshot_AndRestoresIt()
    {
        var crashed = Launch();
        crashed.Dialogs.OpenResult = UserFile("cat.png", Document.CreateBlank(64, 48));
        await crashed.Menu.OpenAsync();
        DrawDot(crashed.Window, 3, 4);
        await TimerFires(crashed);
        Crash(crashed);

        var next = Launch();
        var offer = next.Recovery.OfferRestoreAsync(); // the real prompt
        Dispatcher.UIThread.RunJobs();
        var dialog = Assert.Single(next.Window.OwnedWindows.OfType<RecoveryDialog>());
        Assert.Contains("cat.png", dialog.FindControl<TextBlock>("QuestionText")!.Text);
        dialog.FindControl<Button>("RestoreButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await offer;

        Assert.Equal(Rgba.Black, next.Window.Document.ActiveLayer.Pixels.GetPixel(3, 4));
        Assert.True(next.Window.Editor.HasUnsavedChanges);
        Assert.StartsWith("*Untitled", next.Window.Title);
        Assert.True(next.Recovery.IsTakingSnapshots);

        // The snapshot is now this session's, so it survives another crash, and only this one.
        Assert.True(File.Exists(next.Recovery.Store.SnapshotPath));
        Assert.False(File.Exists(crashed.Recovery.Store.SnapshotPath));
        Crash(next);
        var third = Launch();
        third.Recovery.Prompt = new FakePrompt(RecoveryChoice.Later);
        await third.Recovery.OfferRestoreAsync();
        Assert.Equal(next.Recovery.Store.SessionId, Assert.Single(((FakePrompt)third.Recovery.Prompt).Asked).Id);
    }

    [AvaloniaFact]
    public async Task TheRealPrompt_DiscardDeletesTheSnapshot_AndClosingItLeavesItForNextTime()
    {
        var crashed = Launch();
        DrawDot(crashed.Window, 3, 4);
        await TimerFires(crashed);
        Crash(crashed);

        var later = Launch();
        var offer = later.Recovery.OfferRestoreAsync();
        Dispatcher.UIThread.RunJobs();
        var dialog = Assert.Single(later.Window.OwnedWindows.OfType<RecoveryDialog>());
        Assert.Contains("Untitled", dialog.FindControl<TextBlock>("QuestionText")!.Text);
        dialog.Close();
        await offer;
        Assert.False(later.Window.Editor.HasUnsavedChanges);
        Assert.True(File.Exists(crashed.Recovery.Store.SnapshotPath));
        Crash(later);

        var discard = Launch();
        offer = discard.Recovery.OfferRestoreAsync();
        Dispatcher.UIThread.RunJobs();
        dialog = Assert.Single(discard.Window.OwnedWindows.OfType<RecoveryDialog>());
        dialog.FindControl<Button>("DiscardButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await offer;

        Assert.False(File.Exists(crashed.Recovery.Store.SnapshotPath));
        Assert.False(discard.Window.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task RestoringOverUnsavedChanges_AsksToSaveThemFirst_AndCancelKeepsTheSnapshot()
    {
        var crashed = Launch();
        DrawDot(crashed.Window, 3, 4);
        await TimerFires(crashed);
        Crash(crashed);

        var next = Launch();
        DrawDot(next.Window, 10, 10);
        var saveChanges = new FakeSaveChangesPrompt(SaveChangesChoice.Cancel);
        next.Menu.SaveChangesPrompt = saveChanges;
        next.Recovery.Prompt = new FakePrompt(RecoveryChoice.Restore);
        var before = next.Window.Document;

        await next.Recovery.OfferRestoreAsync();

        Assert.Equal(1, saveChanges.Asked);
        Assert.Same(before, next.Window.Document);
        Assert.True(File.Exists(crashed.Recovery.Store.SnapshotPath));
    }

    [AvaloniaFact]
    public async Task ADamagedSnapshot_IsReported_AndTheDocumentStays()
    {
        var crashed = Launch();
        DrawDot(crashed.Window, 3, 4);
        await TimerFires(crashed);
        Crash(crashed);
        File.WriteAllBytes(crashed.Recovery.Store.SnapshotPath, "PK\u0003\u0004 damaged"u8.ToArray());

        var next = Launch();
        next.Recovery.Prompt = new FakePrompt(RecoveryChoice.Restore);
        var before = next.Window.Document;
        await next.Recovery.OfferRestoreAsync();

        Assert.Same(before, next.Window.Document);
        Assert.Contains("could not be restored", Assert.Single(next.Dialogs.Errors));
    }

    [AvaloniaFact]
    public void OutOfMemory_WritesAnEmergencySnapshot_BesideTheCrashLog()
    {
        var crashes = new CrashHandler(new CrashLog(Path.Combine(_root, "CrashLogs")));
        var session = Launch(crashes);
        DrawDot(session.Window, 3, 4);
        DrawDot(session.Window, 5, 6); // newer than any periodic snapshot: none has been taken

        crashes.Handle(new OutOfMemoryException(), "UI thread");

        Assert.Single(Directory.GetFiles(Path.Combine(_root, "CrashLogs")));
        var snapshot = OpenSnapshot(session.Recovery.Store.SnapshotPath);
        Assert.Equal(Rgba.Black, snapshot.ActiveLayer.Pixels.GetPixel(5, 6));
    }

    [AvaloniaFact]
    public void ACrashWithoutUnsavedChanges_WritesNoSnapshot()
    {
        var crashes = new CrashHandler(new CrashLog(Path.Combine(_root, "CrashLogs")));
        var session = Launch(crashes);

        crashes.Handle(new OutOfMemoryException(), "UI thread");

        Assert.False(File.Exists(session.Recovery.Store.SnapshotPath));
    }
}
