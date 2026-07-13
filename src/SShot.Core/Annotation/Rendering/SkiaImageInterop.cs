using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SShot.Core.Annotation.Rendering;

internal static class SkiaImageInterop
{
    public static SKBitmap ToSKBitmap(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        converted.CopyPixels(new Int32Rect(0, 0, width, height), bitmap.GetPixels(), bitmap.RowBytes * height, bitmap.RowBytes);
        return bitmap;
    }

    public static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        var result = BitmapSource.Create(
            bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32, null,
            bitmap.GetPixels(), bitmap.RowBytes * bitmap.Height, bitmap.RowBytes);
        result.Freeze();
        return result;
    }
}
