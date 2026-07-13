using System.Windows;
using System.Windows.Media;
using SShot.Core.Interop;

namespace SShot.Core.Capture.Dpi;

/// <summary>
/// The only place physical-pixel &lt;-&gt; WPF DIP conversion happens. Two distinct needs:
/// - Pre-show: position/size a not-yet-visible overlay Window in DIPs so it lands exactly
///   on a target monitor, using that monitor's own DPI (via Shcore, since the Window has
///   no PresentationSource yet and can't report its own DPI).
/// - Post-show: convert runtime mouse coordinates using the Window's actual DPI, via
///   VisualTreeHelper.GetDpi (correct once Per-Monitor V2 has attached the window to a monitor).
/// </summary>
public static class DpiHelper
{
    public static double GetDpiScaleForMonitor(Int32Rect physicalBounds)
    {
        var rect = new User32.RECT
        {
            Left = physicalBounds.X,
            Top = physicalBounds.Y,
            Right = physicalBounds.X + physicalBounds.Width,
            Bottom = physicalBounds.Y + physicalBounds.Height,
        };

        IntPtr hMonitor = User32.MonitorFromRect(ref rect, User32.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            return 1.0;
        }

        int hr = Shcore.GetDpiForMonitor(hMonitor, Shcore.MonitorDpiType.EffectiveDpi, out uint dpiX, out _);
        return hr == 0 ? dpiX / 96.0 : 1.0;
    }

    public static double GetDpiScale(Visual visual) => VisualTreeHelper.GetDpi(visual).DpiScaleX;

    public static Rect PhysicalToDip(Int32Rect physicalRect, double dpiScale) => new(
        physicalRect.X / dpiScale,
        physicalRect.Y / dpiScale,
        physicalRect.Width / dpiScale,
        physicalRect.Height / dpiScale);

    public static Point PhysicalToDip(Point physicalPoint, double dpiScale) => new(
        physicalPoint.X / dpiScale,
        physicalPoint.Y / dpiScale);

    public static Int32Rect DipToPhysical(Rect dipRect, double dpiScale) => new(
        (int)Math.Round(dipRect.X * dpiScale),
        (int)Math.Round(dipRect.Y * dpiScale),
        (int)Math.Round(dipRect.Width * dpiScale),
        (int)Math.Round(dipRect.Height * dpiScale));

    public static Point DipToPhysical(Point dipPoint, double dpiScale) => new(
        dipPoint.X * dpiScale,
        dipPoint.Y * dpiScale);
}
