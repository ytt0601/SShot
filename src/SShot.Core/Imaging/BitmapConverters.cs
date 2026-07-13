using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SShot.Core.Imaging;

internal static class BitmapConverters
{
    public static BitmapSource FromHBitmap(IntPtr hBitmap)
    {
        var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        // BitBlt-captured DDBs don't carry a meaningful alpha byte (GDI never writes one), but
        // CreateBitmapSourceFromHBitmap can still hand back a format with an alpha channel
        // (e.g. Pbgra32) whose unwritten 4th byte reads as 0 - rendering the capture partially
        // or fully transparent. Force a strictly opaque format so every capture is always fully
        // opaque, regardless of how WIC happened to interpret the source DDB.
        if (bitmapSource.Format != PixelFormats.Bgr32)
        {
            bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgr32, null, 0);
        }

        bitmapSource.Freeze();
        return bitmapSource;
    }
}
