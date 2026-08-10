using System.Windows;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SShot.Core.Annotation.Rendering;

public static class SkiaBlurRenderer
{
    /// <summary>
    /// Crops a padded region around the requested bounds (pad = 2x radius) so the blur kernel
    /// has real pixels to sample at the edges, then draws the padded source shifted so the
    /// requested region lands at (0,0) in a surface sized to exactly that region - anything
    /// outside is naturally clipped by the surface bounds. Without the padding, a plain
    /// crop-then-blur leaves a transparent halo at the patch edges (the kernel would sample
    /// past the crop into nothing).
    /// </summary>
    public static BitmapSource Apply(BitmapSource source, Int32Rect region, double radius)
    {
        int pad = (int)Math.Ceiling(radius * 2);
        int paddedX = Math.Max(0, region.X - pad);
        int paddedY = Math.Max(0, region.Y - pad);
        int paddedRight = Math.Min(source.PixelWidth, region.X + region.Width + pad);
        int paddedBottom = Math.Min(source.PixelHeight, region.Y + region.Height + pad);
        var paddedRegion = new Int32Rect(paddedX, paddedY, paddedRight - paddedX, paddedBottom - paddedY);

        var cropped = new CroppedBitmap(source, paddedRegion);
        using var skSource = SkiaImageInterop.ToSKBitmap(cropped);

        using var surface = SKSurface.Create(new SKImageInfo(region.Width, region.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            // Clamp: at the image border the padded crop runs out of real pixels; without it the
            // kernel samples transparent (decal), leaving alpha < 255 fringes on edge-touching blurs.
            ImageFilter = SKImageFilter.CreateBlur((float)radius, (float)radius, SKShaderTileMode.Clamp),
        };
        float offsetX = region.X - paddedX;
        float offsetY = region.Y - paddedY;
        canvas.DrawBitmap(skSource, -offsetX, -offsetY, new SKSamplingOptions(SKFilterMode.Linear), paint);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var resultBitmap = SKBitmap.FromImage(image);

        return SkiaImageInterop.ToBitmapSource(resultBitmap);
    }
}
