using SShot.Core.Capture.Dpi;
using SShot.Core.Models;

namespace SShot.Core.Capture;

public sealed class FullScreenCaptureService(IScreenCaptureService screenCapture) : ICaptureService
{
    public Task<CaptureResult?> CaptureAsync()
    {
        var bounds = VirtualScreenBounds.GetVirtualDesktopBounds();
        var image = screenCapture.CaptureRect(bounds);
        return Task.FromResult<CaptureResult?>(new CaptureResult(image, bounds));
    }
}
