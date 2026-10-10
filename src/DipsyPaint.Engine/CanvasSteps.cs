using System.Runtime.CompilerServices;

namespace DipsyPaint.Engine;

/// <summary>
/// A history step for an operation that replaced every layer's pixels and possibly the canvas size
/// (crop, resize). It holds the other version of every layer; undo and redo swap it with the
/// document's, like <see cref="PixelRecorder"/>'s steps do for regions.
/// </summary>
internal sealed class CanvasPixelsStep : HistoryStep
{
    private readonly Document _document;
    private Rgba[][] _stored;
    private (int Width, int Height) _storedSize;

    /// <param name="document">The document after the operation.</param>
    /// <param name="before">Each layer's pixels before it.</param>
    /// <param name="beforeSize">The canvas size before it.</param>
    public CanvasPixelsStep(Document document, Rgba[][] before, (int Width, int Height) beforeSize)
    {
        _document = document;
        _stored = before;
        _storedSize = beforeSize;
        // Constant for the history's accounting: the larger of the two versions it alternates between.
        var largest = Math.Max((long)beforeSize.Width * beforeSize.Height, (long)document.Width * document.Height);
        SizeBytes = largest * before.Length * Unsafe.SizeOf<Rgba>();
    }

    public override long SizeBytes { get; }

    public override IntRect Undo() => Swap();

    public override IntRect Redo() => Swap();

    private IntRect Swap()
    {
        var current = (_document.Width, _document.Height);
        var stored = _stored;
        _stored = _document.ReplacePixels(_storedSize.Width, _storedSize.Height, (i, _) => stored[i]);
        _storedSize = current;
        return new IntRect(0, 0, _document.Width, _document.Height);
    }
}

/// <summary>
/// A history step for a rotation or flip of the whole document. These are exact pixel permutations,
/// so the step stores no pixels: undo applies the inverse transform.
/// </summary>
internal sealed class CanvasTransformStep(Document document, ImageTransform transform) : HistoryStep
{
    public override long SizeBytes => 0;

    public override IntRect Undo() => Apply(document, transform.Inverse());

    public override IntRect Redo() => Apply(document, transform);

    /// <summary>Rotates or flips every layer of <paramref name="document"/>; returns the new canvas rectangle.</summary>
    public static IntRect Apply(Document document, ImageTransform transform)
    {
        var (width, height) = (document.Width, document.Height);
        var (newWidth, newHeight) = transform.SwapsSides() ? (height, width) : (width, height);
        document.ReplacePixels(newWidth, newHeight, (_, pixels) => PixelOps.Transform(pixels, width, height, transform));
        return new IntRect(0, 0, newWidth, newHeight);
    }
}
