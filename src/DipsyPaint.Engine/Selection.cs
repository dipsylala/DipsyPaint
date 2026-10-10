namespace DipsyPaint.Engine;

/// <summary>
/// The current selection (see <see cref="DocumentEditor.Selection"/>): where it is, in document
/// pixels, and its pixels once lifted off the layer (row-major, premultiplied,
/// <paramref name="Bounds"/>-sized), else null, in which case its pixels are the active layer's
/// within <paramref name="Bounds"/>. A lifted selection may extend past the canvas. The floating
/// pixels are the array shown in the overlay; callers must not change them.
/// </summary>
public readonly record struct Selection(IntRect Bounds, Rgba[]? FloatingPixels);
