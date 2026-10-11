using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Images;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

/// <summary>Freeform rotate (rotate handle, Custom rotate) and skew (Resize and skew) in the shell (ticket 23).</summary>
public class FreeTransformTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private sealed class FakeDialogs : IImageDialogs
    {
        public ResizeAndSkew? Resize { get; set; }
        public int? Rotation { get; set; }
        public List<string> Errors { get; } = [];

        public Task<ResizeAndSkew?> AskResizeAsync(int width, int height) => Task.FromResult(Resize);

        public Task<int?> AskRotationAsync() => Task.FromResult(Rotation);

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Shows a 64×48 white document with a red 20×20 square at (10, 10), with the selection tool active.</summary>
    private static (MainWindow Window, ImageControl Image, FakeDialogs Dialogs) ShowWindow()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == SelectionStrings.Select)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); // the command bar may rewrap for the tool's options, moving the canvas
        var image = window.FindControl<ImageControl>("ImageGroup")!;
        var dialogs = new FakeDialogs();
        image.Dialogs = dialogs;
        return (window, image, dialogs);
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static void Click(ImageControl image, string name) =>
        image.FindControl<MenuItem>(name)!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static (int, int) SizeOf(MainWindow window) => (window.Document.Width, window.Document.Height);

    [AvaloniaFact]
    public void CustomRotate_IsInTheRotateMenu_AndTurnsTheSelection_WhichUndoRestores()
    {
        var (window, image, dialogs) = ShowWindow();
        Drag(window, (10.5, 10.5), (29.5, 19.5)); // the top half of the red square: 20×10
        dialogs.Rotation = 90;

        Click(image, "CustomRotateItem");

        Assert.Equal(TransformStrings.CustomRotate, AutomationProperties.GetName(image.FindControl<MenuItem>("CustomRotateItem")!));
        Assert.Equal(new IntRect(15, 5, 10, 20), window.Editor.Selection!.Value.Bounds);
        window.Editor.CommitPending();
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(15, 5));
        Assert.Equal((64, 48), SizeOf(window));

        window.Editor.Undo();
        Assert.Equal(Rgba.White, window.Document.ActiveLayer.Pixels.GetPixel(15, 5));
        Assert.Equal(Red, window.Document.ActiveLayer.Pixels.GetPixel(10, 10));
    }

    [AvaloniaFact]
    public void CustomRotate_WithoutASelection_TurnsThePicture_GrowingTheCanvas()
    {
        var (window, image, dialogs) = ShowWindow();
        dialogs.Rotation = 30;

        Click(image, "CustomRotateItem");

        Assert.Equal((80, 74), SizeOf(window)); // 64 cos 30° + 48 sin 30° by 64 sin 30° + 48 cos 30°
        Assert.Equal((80, 74), (window.Editor.Compositor.Width, window.Editor.Compositor.Height));
        window.Editor.Undo();
        Assert.Equal((64, 48), SizeOf(window));
    }

    [AvaloniaFact]
    public void CustomRotate_Cancelled_ChangesNothing()
    {
        var (window, image, _) = ShowWindow();

        Click(image, "CustomRotateItem");

        Assert.Equal((64, 48), SizeOf(window));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void CustomRotate_TooLarge_IsRefusedWithAMessage()
    {
        var window = new MainWindow(Document.CreateBlank(400, Document.MaxSide));
        window.Show();
        var image = window.FindControl<ImageControl>("ImageGroup")!;
        var dialogs = new FakeDialogs { Rotation = 1 };
        image.Dialogs = dialogs;

        Click(image, "CustomRotateItem");

        Assert.Equal([string.Format(TransformStrings.TooLarge, Document.MaxSide)], dialogs.Errors);
        Assert.Equal((400, Document.MaxSide), SizeOf(window)); // 16384 cos 1° + 400 sin 1° > 16384
    }

    [AvaloniaFact]
    public void ResizeAndSkew_SkewsThePicture_AsOneStep()
    {
        var (window, image, dialogs) = ShowWindow();
        dialogs.Resize = new ResizeAndSkew(64, 48, 20, 0);

        image.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == ImageStrings.Resize)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal((82, 48), SizeOf(window)); // 64 + 48 tan 20° = 81.5
        window.Editor.Undo();
        Assert.Equal((64, 48), SizeOf(window));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ResizeDialog_ReturnsTheSkew_WithinPaintsRange()
    {
        var dialog = new ResizeDialog(64, 48);
        dialog.Show();
        var horizontal = dialog.FindControl<NumericUpDown>("HorizontalSkewBox")!;
        var vertical = dialog.FindControl<NumericUpDown>("VerticalSkewBox")!;
        Assert.Equal((0m, 0m), (horizontal.Value, vertical.Value));
        Assert.Equal((-89m, 89m), (horizontal.Minimum, horizontal.Maximum));
        Assert.Equal(TransformStrings.VerticalSkew, AutomationProperties.GetName(vertical));

        horizontal.Value = 20;
        vertical.Value = -15;
        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(new ResizeAndSkew(64, 48, 20, -15), dialog.Result);
    }

    [AvaloniaFact]
    public void CustomRotateDialog_ReturnsTheAngle_OrNothingWhenCancelled()
    {
        var dialog = new CustomRotateDialog();
        dialog.Show();
        dialog.FindControl<NumericUpDown>("AngleBox")!.Value = 137;
        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(137, dialog.Result);

        var cancelled = new CustomRotateDialog();
        cancelled.Show();
        cancelled.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(cancelled.Result);
    }

    [AvaloniaFact]
    public void RotateHandle_AboveTheCanvas_TakesPresses_AndDraggingItTurnsTheSelection()
    {
        var (window, _, _) = ShowWindow();
        Drag(window, (0.5, 0.5), (39.5, 9.5)); // 40×10 along the top edge: the handle is above the canvas
        var editor = window.Editor;
        var (hx, hy) = editor.Adornment!.Value.RotateHandle(editor.HandleTolerance);
        Assert.True(hy < 0);

        // From above the centre (20, 5) to its right: a quarter turn clockwise.
        Drag(window, (hx, hy), (45, 5));

        Assert.Equal(new IntRect(15, -15, 10, 40), editor.Selection!.Value.Bounds);
        Assert.False(editor.History.CanUndo); // still pending
    }
}
