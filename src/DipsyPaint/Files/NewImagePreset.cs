using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>
/// A size offered by the New image dialog's Size dropdown, in its standard orientation, with the
/// resolution it creates (ticket 41, ADR-0024).
/// </summary>
/// <param name="Name">Shown after the size, e.g. "Full HD"; null for none.</param>
public sealed record NewImagePreset(int Width, int Height, Resolution Resolution, string? Name = null)
{
    private static readonly Resolution Print = new(300, 300);

    /// <summary>"1920 × 1080 px (Full HD)", or just the size when there's no name.</summary>
    public string Text
    {
        get
        {
            var size = string.Format(NewImageStrings.SizeFormat, Width, Height);
            return Name is null ? size : string.Format(NewImageStrings.NamedSizeFormat, size, Name);
        }
    }

    /// <summary>The dropdown's presets in order, in groups under a heading; the first group has none.</summary>
    public static IReadOnlyList<(string? Heading, NewImagePreset[] Presets)> Groups() =>
    [
        (null, [new(Document.DefaultWidth, Document.DefaultHeight, Resolution.Default, NewImageStrings.Default)]),
        (NewImageStrings.ScreenGroup,
        [
            new(1280, 720, Resolution.Default, NewImageStrings.Hd),
            new(1920, 1080, Resolution.Default, NewImageStrings.FullHd),
            new(2560, 1440, Resolution.Default, NewImageStrings.Qhd),
            new(3840, 2160, Resolution.Default, NewImageStrings.Uhd4k),
        ]),
        (NewImageStrings.SquareGroup,
        [
            new(1080, 1080, Resolution.Default),
            new(512, 512, Resolution.Default),
            new(256, 256, Resolution.Default),
        ]),
        (NewImageStrings.PortraitGroup, [new(1080, 1920, Resolution.Default)]),
        (NewImageStrings.PrintGroup,
        [
            new(2480, 3508, Print, string.Format(NewImageStrings.A4, ResolutionText.Format(Print))),
            new(2550, 3300, Print, string.Format(NewImageStrings.UsLetter, ResolutionText.Format(Print))),
        ]),
    ];
}
