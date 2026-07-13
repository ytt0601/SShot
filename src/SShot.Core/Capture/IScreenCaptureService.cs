using System.Windows;
using System.Windows.Media.Imaging;

namespace SShot.Core.Capture;

/// <summary>Low-level primitive: grab a physical-pixel, virtual-desktop-relative rect from the screen.</summary>
public interface IScreenCaptureService
{
    BitmapSource CaptureRect(Int32Rect physicalPixelRect);
}
