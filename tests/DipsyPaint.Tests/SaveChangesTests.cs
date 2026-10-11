using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Codecs;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Files;
using DipsyPaint.Updates;

namespace DipsyPaint.Tests;

public sealed class SaveChangesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-save-changes-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class FakePrompt : ISaveChangesPrompt
    {
        public SaveChangesChoice Choice { get; set; } = SaveChangesChoice.Cancel;

        /// <summary>When set, the prompt stays open until this completes.</summary>
        public TaskCompletionSource<SaveChangesChoice>? Pending { get; set; }

        public List<string> Asked { get; } = [];

        public Task<SaveChangesChoice> AskAsync(string documentName)
        {
            Asked.Add(documentName);
            return Pending?.Task ?? Task.FromResult(Choice);
        }
    }

    private sealed class FakeDialogs : IFileDialogs
    {
        public PickedFile? OpenResult { get; set; }
        public PickedFile? SaveResult { get; set; }
        public int OpenRequests { get; private set; }
        public int SaveRequests { get; private set; }
        public int NewSizeRequests { get; private set; }
        public List<string> Errors { get; } = [];

        public Task<(int Width, int Height)?> AskNewImageSizeAsync(int width, int height)
        {
            NewSizeRequests++;
            return Task.FromResult<(int, int)?>((width, height));
        }

        public Task<PickedFile?> PickOpenAsync()
        {
            OpenRequests++;
            return Task.FromResult(OpenResult);
        }

        public Task<PickedFile?> PickSaveAsync(string suggestedName, ImageFormat format)
        {
            SaveRequests++;
            return Task.FromResult(SaveResult);
        }

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        /// <summary>The answer to the lossy-save warning (palette formats).</summary>
        public bool ConfirmLossySave { get; set; } = true;

        public Task<bool> ConfirmLossySaveAsync() => Task.FromResult(ConfirmLossySave);
    }

    private sealed class FakeUpdater : IUpdater
    {
        public int Applied { get; private set; }

        public Task<bool> DownloadUpdateAsync() => Task.FromResult(true);

        public void ApplyAfterExitAndRestart() => Applied++;
    }

    private sealed record Setup(MainWindow Window, FileMenu Menu, FakeDialogs Dialogs, FakePrompt Prompt);

    private Setup ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var setup = new Setup(window, menu, new FakeDialogs(), new FakePrompt());
        menu.Dialogs = setup.Dialogs;
        menu.SaveChangesPrompt = setup.Prompt;
        setup.Dialogs.OpenResult = FileOnDisk("other.png", Png(Document.CreateBlank(20, 10)));
        return setup;
    }

    private PickedFile FileOnDisk(string name, byte[]? content = null)
    {
        var path = Path.Combine(_folder, name);
        if (content is not null)
        {
            File.WriteAllBytes(path, content);
        }

        return new PickedFile(name, () => Task.FromResult<Stream>(File.OpenRead(path)), () => Task.FromResult<Stream>(File.Create(path)));
    }

    private static PickedFile UnwritableFile(string name) =>
        new(name, () => throw new IOException(), () => Task.FromException<Stream>(new UnauthorizedAccessException()));

    private static byte[] Png(Document document)
    {
        using var stream = new MemoryStream();
        ImageCodec.Export(new Compositor(document), stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static void DrawDot(MainWindow window, int x, int y)
    {
        window.Editor.Apply(new PointerInput(PointerAction.Down, x + 0.5, y + 0.5, PointerButton.Left));
        window.Editor.Apply(new PointerInput(PointerAction.Up, x + 0.5, y + 0.5, PointerButton.Left));
    }

    // Lets the window's async Closing handler (prompt, save, close again) run to the end.
    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
        }
    }

    /// <summary>Runs <paramref name="trigger"/>; returns whether the document was discarded (replaced or closed).</summary>
    private static async Task<bool> Run(string trigger, Setup setup)
    {
        var (window, menu, _, _) = setup;
        var document = window.Document;
        switch (trigger)
        {
            case "new":
                await menu.NewAsync();
                return window.Document != document;
            case "open":
                await menu.OpenAsync();
                return window.Document != document;
            case "close":
                window.Close();
                await Settle();
                return !window.IsVisible;
            case "restart":
                var updates = window.FindControl<UpdateControl>("Updates")!;
                var updater = new FakeUpdater();
                await updates.CheckAsync(updater);
                await updates.RestartToUpdateAsync();
                Assert.Equal(window.IsVisible ? 0 : 1, updater.Applied); // applies only when it closed
                return !window.IsVisible;
            default:
                throw new ArgumentOutOfRangeException(nameof(trigger));
        }
    }

    public static TheoryData<string> Triggers => ["new", "open", "close", "restart"];

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task WithoutUnsavedChanges_TheTriggerGoesAheadWithoutAsking(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        setup.Window.Editor.Undo(); // back to the saved (new) state

        Assert.True(await Run(trigger, setup));

        Assert.Empty(setup.Prompt.Asked);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task Save_SavesAsForAnUntitledDocument_ThenGoesAhead(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        setup.Prompt.Choice = SaveChangesChoice.Save;
        setup.Dialogs.SaveResult = FileOnDisk("saved.png");

        Assert.True(await Run(trigger, setup));

        Assert.Equal(["Untitled"], setup.Prompt.Asked);
        Assert.Equal(1, setup.Dialogs.SaveRequests);
        using var saved = File.OpenRead(Path.Combine(_folder, "saved.png"));
        Assert.Equal(Rgba.Black, ImageCodec.Open(saved).ActiveLayer.Pixels.GetPixel(1, 1));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task DontSave_GoesAheadWithoutSaving(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        setup.Prompt.Choice = SaveChangesChoice.DontSave;

        Assert.True(await Run(trigger, setup));

        Assert.Single(setup.Prompt.Asked);
        Assert.Equal(0, setup.Dialogs.SaveRequests);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task Cancel_LeavesEverythingAsItWas(string trigger)
    {
        var setup = ShowWindow();
        var window = setup.Window;
        DrawDot(window, 1, 1);
        var document = window.Document;
        setup.Prompt.Choice = SaveChangesChoice.Cancel;

        Assert.False(await Run(trigger, setup));

        Assert.Single(setup.Prompt.Asked);
        Assert.Same(document, window.Document);
        Assert.True(window.IsVisible);
        Assert.True(window.Editor.HasUnsavedChanges);
        Assert.True(window.Editor.History.CanUndo);
        Assert.Equal("*Untitled - DipsyPaint", window.Title);
        Assert.Equal((0, 0, 0, 0), (setup.Dialogs.SaveRequests, setup.Dialogs.OpenRequests, setup.Dialogs.NewSizeRequests, setup.Dialogs.Errors.Count));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task Save_ThenCancellingSaveAs_KeepsTheDocument(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        var document = setup.Window.Document;
        setup.Prompt.Choice = SaveChangesChoice.Save;
        setup.Dialogs.SaveResult = null;

        Assert.False(await Run(trigger, setup));

        Assert.Equal(1, setup.Dialogs.SaveRequests);
        Assert.Same(document, setup.Window.Document);
        Assert.True(setup.Window.Editor.HasUnsavedChanges);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task Save_ThatFails_KeepsTheDocument(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        var document = setup.Window.Document;
        setup.Prompt.Choice = SaveChangesChoice.Save;
        setup.Dialogs.SaveResult = UnwritableFile("locked.png");

        Assert.False(await Run(trigger, setup));

        Assert.StartsWith("Could not save", Assert.Single(setup.Dialogs.Errors));
        Assert.Same(document, setup.Window.Document);
        Assert.True(setup.Window.Editor.HasUnsavedChanges);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public async Task Save_ThenCancellingTheLossySaveWarning_KeepsTheDocument(string trigger)
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        var document = setup.Window.Document;
        setup.Prompt.Choice = SaveChangesChoice.Save;
        setup.Dialogs.SaveResult = FileOnDisk("palette.gif");
        setup.Dialogs.ConfirmLossySave = false;

        Assert.False(await Run(trigger, setup));

        Assert.Same(document, setup.Window.Document);
        Assert.True(setup.Window.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task Save_WritesToTheCurrentFileWithoutAsking_AndThePromptNamesIt()
    {
        var setup = ShowWindow();
        await setup.Menu.OpenAsync(); // other.png
        DrawDot(setup.Window, 2, 2);
        setup.Prompt.Choice = SaveChangesChoice.Save;
        setup.Dialogs.OpenResult = FileOnDisk("third.png", Png(Document.CreateBlank(5, 5)));

        await setup.Menu.OpenAsync();

        Assert.Equal(["other.png"], setup.Prompt.Asked);
        Assert.Equal(0, setup.Dialogs.SaveRequests);
        Assert.Equal((5, 5), (setup.Window.Document.Width, setup.Window.Document.Height));
        using var saved = File.OpenRead(Path.Combine(_folder, "other.png"));
        Assert.Equal(Rgba.Black, ImageCodec.Open(saved).ActiveLayer.Pixels.GetPixel(2, 2));
    }

    [AvaloniaFact]
    public async Task AskingIsBeforeTheNewAndOpenDialogs()
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        setup.Prompt.Pending = new TaskCompletionSource<SaveChangesChoice>();

        var opening = setup.Menu.OpenAsync();
        Assert.Single(setup.Prompt.Asked);
        Assert.Equal(0, setup.Dialogs.OpenRequests);

        setup.Prompt.Pending.SetResult(SaveChangesChoice.DontSave);
        await opening;
        Assert.Equal(1, setup.Dialogs.OpenRequests);
    }

    [AvaloniaFact]
    public async Task ASecondCloseWhileThePromptIsShowing_DoesNotAskAgain()
    {
        var setup = ShowWindow();
        DrawDot(setup.Window, 1, 1);
        setup.Prompt.Pending = new TaskCompletionSource<SaveChangesChoice>();

        setup.Window.Close();
        setup.Window.Close(); // e.g. Cmd+Q while the prompt is up
        Assert.Single(setup.Prompt.Asked);
        Assert.True(setup.Window.IsVisible);

        setup.Prompt.Pending.SetResult(SaveChangesChoice.DontSave);
        await Settle();
        Assert.False(setup.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task APendingShape_CountsAsAChangeToLose()
    {
        var setup = ShowWindow();
        var editor = setup.Window.Editor;
        editor.ActiveTool = new ShapeTool(new ShapeSettings { Kind = ShapeKind.Rectangle });
        editor.Apply(new PointerInput(PointerAction.Down, 2.5, 2.5));
        editor.Apply(new PointerInput(PointerAction.Move, 20.5, 20.5));
        editor.Apply(new PointerInput(PointerAction.Up, 20.5, 20.5));
        Assert.False(editor.HasUnsavedChanges); // not a history step yet
        setup.Prompt.Choice = SaveChangesChoice.Cancel;

        await setup.Menu.NewAsync();

        Assert.Single(setup.Prompt.Asked);
        Assert.NotNull(editor.Adornment); // still pending
    }

    [AvaloniaFact]
    public async Task ASelectionThatHasNotBeenMoved_IsNotAChange()
    {
        var setup = ShowWindow();
        var editor = setup.Window.Editor;
        editor.ActiveTool = new SelectionTool();
        editor.Apply(new PointerInput(PointerAction.Down, 2.5, 2.5));
        editor.Apply(new PointerInput(PointerAction.Move, 20.5, 20.5));
        editor.Apply(new PointerInput(PointerAction.Up, 20.5, 20.5));
        Assert.NotNull(editor.Selection);

        await setup.Menu.NewAsync();

        Assert.Empty(setup.Prompt.Asked);
    }

    [AvaloniaFact]
    public async Task TheTitleMarksUnsavedChanges()
    {
        var setup = ShowWindow();
        var window = setup.Window;
        Assert.Equal("Untitled - DipsyPaint", window.Title);

        DrawDot(window, 1, 1);
        Assert.Equal("*Untitled - DipsyPaint", window.Title);

        window.Editor.Undo();
        Assert.Equal("Untitled - DipsyPaint", window.Title);

        window.Editor.Redo();
        setup.Dialogs.SaveResult = FileOnDisk("art.png");
        await setup.Menu.SaveAsync();
        Assert.Equal("art.png - DipsyPaint", window.Title);

        DrawDot(window, 2, 2);
        Assert.Equal("*art.png - DipsyPaint", window.Title);

        window.Editor.Undo();
        Assert.Equal("art.png - DipsyPaint", window.Title);

        window.Editor.Undo(); // past the saved point
        Assert.Equal("*art.png - DipsyPaint", window.Title);

        setup.Prompt.Choice = SaveChangesChoice.DontSave;
        await setup.Menu.NewAsync();
        Assert.Equal("Untitled - DipsyPaint", window.Title);
    }

    [AvaloniaFact]
    public void Dialog_WordingButtonsAndAutomationNamesMatchReferencePaint()
    {
        var dialog = new SaveChangesDialog("photo.png");
        dialog.Show();

        Assert.Equal("DipsyPaint", dialog.Title);
        Assert.Equal("Do you want to save changes to photo.png?", dialog.FindControl<TextBlock>("QuestionText")!.Text);
        foreach (var (name, text) in new[] { ("SaveButton", "Save"), ("DontSaveButton", "Don't save"), ("CancelButton", "Cancel") })
        {
            var button = dialog.FindControl<Button>(name)!;
            Assert.Equal(text, button.Content);
            Assert.Equal(text, AutomationProperties.GetName(button));
        }

        Assert.True(dialog.FindControl<Button>("SaveButton")!.IsFocused);
    }

    [AvaloniaTheory]
    [InlineData("SaveButton", SaveChangesChoice.Save)]
    [InlineData("DontSaveButton", SaveChangesChoice.DontSave)]
    [InlineData("CancelButton", SaveChangesChoice.Cancel)]
    public void Dialog_EachButtonClosesWithItsChoice(string button, SaveChangesChoice expected)
    {
        var dialog = new SaveChangesDialog("Untitled");
        dialog.Show();

        dialog.FindControl<Button>(button)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(expected, dialog.Result);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter, SaveChangesChoice.Save)]
    [InlineData(Key.Escape, SaveChangesChoice.Cancel)]
    public void Dialog_EnterSaves_AndEscCancels(Key key, SaveChangesChoice expected)
    {
        var dialog = new SaveChangesDialog("Untitled");
        dialog.Show();

        dialog.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

        Assert.Equal(expected, dialog.Result);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public void Dialog_ClosedAnyOtherWay_IsCancel()
    {
        var dialog = new SaveChangesDialog("Untitled");
        dialog.Show();

        dialog.Close();

        Assert.Equal(SaveChangesChoice.Cancel, dialog.Result);
    }

    [AvaloniaFact]
    public async Task TheRealPrompt_IsAModalDialogOverTheWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        var menu = window.FindControl<FileMenu>("FileMenu")!;
        var dialogs = new FakeDialogs();
        menu.Dialogs = dialogs;
        DrawDot(window, 1, 1);

        var creating = menu.NewAsync();
        var dialog = Assert.Single(window.OwnedWindows.OfType<SaveChangesDialog>());
        dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
        await creating;

        Assert.Equal(0, dialogs.NewSizeRequests);
        Assert.True(window.Editor.HasUnsavedChanges);
    }
}
