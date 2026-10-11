using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

public class TextToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    // The frame's band around the box at the default handle tolerance.
    private const int Margin = 4;

    /// <summary>
    /// A predictable stand-in for the shell's font engine: each character is a solid
    /// <see cref="CharWidth"/>×<see cref="LineHeight"/> cell, wrapped by character; '\n' breaks the line.
    /// </summary>
    private sealed class BlockRenderer : ITextRenderer
    {
        public const int CharWidth = 4;
        public const int LineHeight = 8;

        public TextRequest? Last { get; private set; }

        public PixelImage Render(TextRequest request)
        {
            Last = request;
            var perLine = Math.Max(1, request.Width / CharWidth);
            var cells = new List<(int Column, int Line)>();
            var (column, line) = (0, 0);
            foreach (var c in request.Text)
            {
                if (c == '\n' || column == perLine)
                {
                    (column, line) = (0, line + 1);
                }

                if (c != '\n')
                {
                    cells.Add((column++, line));
                }
            }

            var height = Math.Max(request.MinHeight, (line + 1) * LineHeight);
            var pixels = new Rgba[request.Width * height];
            Array.Fill(pixels, request.Background ?? Rgba.Transparent);
            foreach (var (cx, cy) in cells)
            {
                for (var y = cy * LineHeight; y < (cy + 1) * LineHeight; y++)
                {
                    for (var x = cx * CharWidth; x < Math.Min(request.Width, (cx + 1) * CharWidth); x++)
                    {
                        pixels[y * request.Width + x] = request.Foreground;
                    }
                }
            }

            return new PixelImage(request.Width, height, pixels);
        }
    }

    private static (DocumentEditor Editor, TextTool Tool, BlockRenderer Renderer) CreateEditor(TextSettings? settings = null)
    {
        var renderer = new BlockRenderer();
        var tool = new TextTool(settings ?? new TextSettings(), renderer);
        var editor = new DocumentEditor(Document.CreateBlank(300, 100))
        {
            ActiveTool = tool,
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };
        return (editor, tool, renderer);
    }

    private static void Type(DocumentEditor editor, TextTool tool, string text)
    {
        tool.Text = text;
        editor.RefreshPending();
    }

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba Shown(DocumentEditor editor, int x, int y) => editor.Compositor.Pixels[y * editor.Compositor.Width + x];

    [Fact]
    public void Click_OpensAnEmptyOneLineBoxOfTheDefaultWidth_WithAFrameAroundIt()
    {
        var (editor, tool, _) = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));

        var box = new IntRect(10, 20, TextTool.DefaultWidth, BlockRenderer.LineHeight);
        Assert.Equal(box, tool.TextBounds);
        Assert.Equal("", tool.Text);
        Assert.Equal(new Adornment(new IntRect(10 - Margin, 20 - Margin, box.Width + 2 * Margin, box.Height + 2 * Margin)), editor.Adornment);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Drag_OpensABoxTheSizeOfTheDraggedRectangle()
    {
        var (editor, tool, _) = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (50.5, 40.5), (10.5, 10.5), (20.5, 30.5)); // drawn up and left

        Assert.Equal(new IntRect(20, 30, 31, 11), tool.TextBounds);
    }

    [Fact]
    public void TypedText_IsShownInThePrimaryColour_ButPendingUntilCommitted()
    {
        var (editor, tool, renderer) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));

        Type(editor, tool, "Hi");

        Assert.Equal(Red, Shown(editor, 10, 20));
        Assert.Equal(Red, Shown(editor, 17, 27));
        Assert.Equal(Rgba.White, Shown(editor, 18, 20));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20));
        Assert.Equal(new TextRequest("Hi", new TextSettings().ToStyle(), TextTool.DefaultWidth, 1, Red, null), renderer.Last);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Commit_WritesTheTextToTheLayer_AsOneStepThatUndoesAndRedoes()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(10, 20));
        Assert.Null(editor.Adornment);
        Assert.Null(tool.TextBounds);
        Assert.True(editor.Overlay.IsEmpty);

        editor.Undo();
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20));
        Assert.False(editor.History.CanUndo);

        editor.Redo();
        Assert.Equal(Red, Layer(editor).GetPixel(17, 27));
    }

    [Fact]
    public void EmptyText_CommitsNothing_EvenWithAFilledBackground()
    {
        var (editor, _, _) = CreateEditor(new TextSettings { FillBackground = true });
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Assert.Equal(Blue, Shown(editor, 10, 20)); // the empty box is shown filled while pending

        editor.CommitPending();

        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20));
        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void PressOutside_CommitsTheText_WithoutOpeningANewBox()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        Script.Stroke(editor, PointerButton.Left, (250.5, 80.5));

        Assert.Equal(Red, Layer(editor).GetPixel(10, 20));
        Assert.Null(editor.Adornment);
        Assert.Null(tool.TextBounds);

        editor.Undo();
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20));
        Assert.False(editor.History.CanUndo);

        Script.Stroke(editor, PointerButton.Left, (250.5, 80.5)); // with nothing pending, a click opens a box
        Assert.Equal(250, tool.TextBounds?.X);
    }

    [Fact]
    public void LongText_WrapsAtTheBoxWidth_AndTheBoxGrowsDownwards()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 10.5), (25.5, 12.5)); // 16 px: four characters a line

        Type(editor, tool, "abcdefghij");

        Assert.Equal(new IntRect(10, 10, 16, 3 * BlockRenderer.LineHeight), tool.TextBounds);
        Assert.Equal(Red, Shown(editor, 10, 26));
        Assert.Equal(Rgba.White, Shown(editor, 18, 26));

        Type(editor, tool, "ab");
        Assert.Equal(new IntRect(10, 10, 16, BlockRenderer.LineHeight), tool.TextBounds);
    }

    [Fact]
    public void DraggingTheFrame_MovesTheBox()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        Script.Stroke(editor, PointerButton.Left, (60.5, 20.5 - Margin + 1), (90.5, 50.5 - Margin + 1)); // the top of the frame

        Assert.Equal(new IntRect(40, 50, TextTool.DefaultWidth, BlockRenderer.LineHeight), tool.TextBounds);
        Assert.Equal(Red, Shown(editor, 40, 50));
        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
        Assert.Equal("Hi", tool.Text);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void DraggingAHandle_ResizesTheBox_AndRewrapsTheText()
    {
        var (editor, tool, renderer) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 10.5), (49.5, 17.5)); // 40 px: ten characters a line
        Type(editor, tool, "abcdefgh");
        var right = 50 + Margin;

        Script.Stroke(editor, PointerButton.Left, (right, 14), (right - 24, 14)); // the right handle, 16 px: four a line

        Assert.Equal(16, renderer.Last!.Width);
        Assert.Equal(new IntRect(10, 10, 16, 2 * BlockRenderer.LineHeight), tool.TextBounds);
    }

    [Fact]
    public void ShiftDraggingACorner_ResizesTheBoxAsWithoutShift()
    {
        IntRect? Resized(bool shift)
        {
            var (editor, tool, _) = CreateEditor();
            Script.Stroke(editor, PointerButton.Left, (10.5, 10.5), (49.5, 17.5));
            Type(editor, tool, "abcdefgh");
            var (right, bottom) = (50 + Margin, 18 + Margin); // the bottom-right handle

            editor.Apply(new PointerInput(PointerAction.Down, right, bottom));
            editor.Apply(new PointerInput(PointerAction.Move, right - 24, bottom + 10, Shift: shift));
            editor.Apply(new PointerInput(PointerAction.Up, right - 24, bottom + 10, Shift: shift));
            return tool.TextBounds;
        }

        Assert.Equal(Resized(shift: false), Resized(shift: true));
        Assert.Equal(16, Resized(shift: true)!.Value.Width);
    }

    [Fact]
    public void Esc_DiscardsTheText()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        editor.CancelPending();

        Assert.Null(tool.TextBounds);
        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(10, 20));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void ColoursAndOptions_ApplyToAllOfThePendingText()
    {
        var settings = new TextSettings();
        var (editor, tool, renderer) = CreateEditor(settings);
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        editor.PrimaryColor = Rgba.Black;
        Assert.Equal(Rgba.Black, Shown(editor, 10, 20));

        settings.FillBackground = true;
        settings.FontFamily = "Some Font";
        settings.Size = 24;
        settings.Bold = settings.Italic = settings.Underline = settings.Strikethrough = true;
        editor.RefreshPending();

        Assert.Equal(new TextStyle("Some Font", 24, true, true, true, true), renderer.Last!.Style);
        Assert.Equal(Blue, Shown(editor, 18, 20)); // background, in the secondary colour
        Assert.Equal(Rgba.Black, Shown(editor, 10, 20));
    }

    [Fact]
    public void UndoWhilePending_CommitsAndUndoes_SoRedoBringsTheTextBack()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        editor.Undo();

        Assert.Equal(Rgba.White, Shown(editor, 10, 20));
        Assert.Null(tool.TextBounds);
        editor.Redo();
        Assert.Equal(Red, Layer(editor).GetPixel(10, 20));
    }

    [Fact]
    public void SwitchingTools_CommitsTheText()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        Type(editor, tool, "Hi");

        editor.ActiveTool = new PencilTool();

        Assert.Equal(Red, Layer(editor).GetPixel(10, 20));
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void RightButton_DoesNothing()
    {
        var (editor, tool, _) = CreateEditor();

        Script.Stroke(editor, PointerButton.Right, (10.5, 20.5), (50.5, 40.5));

        Assert.Null(tool.TextBounds);
        Assert.Null(editor.Adornment);
    }

    [Fact]
    public void TextOffTheCanvas_IsCut_WhenCommitted()
    {
        var (editor, tool, _) = CreateEditor();
        Script.Stroke(editor, PointerButton.Left, (290.5, 95.5));
        Type(editor, tool, "abcdef");

        editor.CommitPending();

        Assert.Equal(Red, Layer(editor).GetPixel(299, 99));
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void Changed_IsRaisedWhenTheBoxAppearsChangesAndGoes()
    {
        var (editor, tool, _) = CreateEditor();
        var raised = 0;
        tool.Changed += (_, _) => raised++;

        Script.Stroke(editor, PointerButton.Left, (10.5, 20.5));
        var afterOpening = raised;
        Type(editor, tool, "Hi");
        var afterTyping = raised;
        editor.CommitPending();

        Assert.True(afterOpening > 0);
        Assert.True(afterTyping > afterOpening);
        Assert.True(raised > afterTyping);
    }

    [Fact]
    public void Settings_RaiseChangedOnlyOnChange_AndClampTheSize()
    {
        var settings = new TextSettings();
        var raised = 0;
        settings.Changed += (_, _) => raised++;

        settings.Bold = true;
        settings.Bold = true;
        settings.Size = 0;
        settings.Size = double.NaN;
        settings.Size = 1000;

        Assert.Equal(3, raised);
        Assert.Equal(TextSettings.MaxSize, settings.Size);
        Assert.Equal(11, new TextSettings().Size);
    }
}
