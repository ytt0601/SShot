using System.Windows;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SShot.Core.Annotation.Rendering;

/// <summary>
/// Pixelates the given region of the source image: downsample to blockSize-sized cells, then
/// upsample back with nearest-neighbor so each cell renders as a single flat block.
/// </summary>
public static class SkiaMosaicRenderer
{
    public static BitmapSource Apply(BitmapSource source, Int32Rect region, int blockSize)
    {
        blockSize = Math.Max(2, blockSize);

        var cropped = new CroppedBitmap(source, region);
        using var full = SkiaImageInterop.ToSKBitmap(cropped);

        int smallWidth = Math.Max(1, region.Width / blockSize);
        int smallHeight = Math.Max(1, region.Height / blockSize);

        using var small = new SKBitmap(new SKImageInfo(smallWidth, smallHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        full.ScalePixels(small, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var pixelated = new SKBitmap(new SKImageInfo(region.Width, region.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        small.ScalePixels(pixelated, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        return SkiaImageInterop.ToBitmapSource(pixelated);
    }
}
