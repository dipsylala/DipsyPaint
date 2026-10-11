using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint.Layers;

/// <summary>
/// One layer in the <see cref="LayersPanel"/>: a thumbnail that selects the layer (with the layer's
/// menu on right-click or Shift+F10), and an eye in its corner that shows or hides it.
/// </summary>
public sealed class LayerRow : Grid
{
    // The thumbnail fits in this box, keeping the canvas's aspect ratio.
    private const int MaxThumbnailWidth = 64;
    private const int MaxThumbnailHeight = 48;

    private const string EyePath = "F1 M8,3 C4,3 1.5,6 0.5,8 C1.5,10 4,13 8,13 C12,13 14.5,10 15.5,8 C14.5,6 12,3 8,3 Z M8,5 A3,3 0 1 1 8,11 A3,3 0 1 1 8,5 Z";
    private static readonly Geometry EyeIcon = Geometry.Parse(EyePath);
    private static readonly Geometry HiddenEyeIcon = Geometry.Parse(EyePath + " M1,2 L2,1 L15,14 L14,15 Z");

    private readonly Image _thumbnail;
    private readonly PathIcon _eyeIcon;
    private WriteableBitmap? _bitmap;

    internal LayerRow(Layer layer)
    {
        Layer = layer;
        _thumbnail = new Image { Stretch = Stretch.None };
        RenderOptions.SetBitmapInterpolationMode(_thumbnail, BitmapInterpolationMode.None);
        Select = new ToggleButton
        {
            Padding = new Thickness(4),
            Content = new Border { Background = DocumentCanvas.Checkerboard, Child = _thumbnail, ClipToBounds = true },
        };
        ToolTip.SetTip(Select, LayerStrings.LayerToolTip);

        _eyeIcon = new PathIcon { Width = 12, Height = 12 };
        Visibility = new ToggleButton
        {
            Width = 22,
            Height = 22,
            Padding = default,
            Margin = new Thickness(0, 2, 2, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = _eyeIcon,
        };
        ToolTip.SetTip(Visibility, LayerStrings.HideLayer);

        Children.Add(Select);
        Children.Add(Visibility); // drawn over the thumbnail's corner
    }

    /// <summary>The layer this row shows.</summary>
    public Layer Layer { get; }

    /// <summary>Selects the layer; checked while it is the active layer. Its context flyout is the layer's menu.</summary>
    public ToggleButton Select { get; }

    /// <summary>Shows or hides the layer; checked while it is visible.</summary>
    public ToggleButton Visibility { get; }

    /// <summary>Shows the layer's state: its position (1 is the bottom), whether it is active, its visibility and its pixels.</summary>
    internal void Update(int number, bool isActive)
    {
        var name = string.Format(LayerStrings.LayerFormat, number);
        AutomationProperties.SetName(Select, name);
        AutomationProperties.SetName(Visibility, string.Format(LayerStrings.LayerVisibleFormat, number));
        Select.IsChecked = isActive;
        Visibility.IsChecked = Layer.IsVisible;
        _eyeIcon.Data = Layer.IsVisible ? EyeIcon : HiddenEyeIcon;
        ToolTip.SetTip(Visibility, Layer.IsVisible ? LayerStrings.HideLayer : LayerStrings.ShowLayer);
        _thumbnail.Opacity = Layer.IsVisible ? 1 : 0.4;
        RenderThumbnail();
    }

    // Samples the layer at the thumbnail's resolution (nearest pixel); the layer's own pixels, so a
    // hidden layer still shows what it holds.
    private unsafe void RenderThumbnail()
    {
        var pixels = Layer.Pixels;
        var scale = Math.Min((double)MaxThumbnailWidth / pixels.Width, (double)MaxThumbnailHeight / pixels.Height);
        var width = Math.Clamp((int)Math.Round(pixels.Width * scale), 1, MaxThumbnailWidth);
        var height = Math.Clamp((int)Math.Round(pixels.Height * scale), 1, MaxThumbnailHeight);
        if (_bitmap is null || _bitmap.PixelSize != new PixelSize(width, height))
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
            _thumbnail.Source = _bitmap;
            _thumbnail.Width = width;
            _thumbnail.Height = height;
        }

        var row = new Rgba[width];
        using (var buffer = _bitmap.Lock())
        {
            for (var y = 0; y < height; y++)
            {
                var sourceY = (int)((y + 0.5) * pixels.Height / height);
                for (var x = 0; x < width; x++)
                {
                    row[x] = pixels.GetPixel((int)((x + 0.5) * pixels.Width / width), sourceY);
                }

                MemoryMarshal.AsBytes(row.AsSpan()).CopyTo(new Span<byte>((byte*)buffer.Address + y * buffer.RowBytes, width * 4));
            }
        }

        _thumbnail.InvalidateVisual();
    }
}
