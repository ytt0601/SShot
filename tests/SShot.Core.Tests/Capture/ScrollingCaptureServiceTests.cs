using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.Capture;

namespace SShot.Core.Tests.Capture;

/// <summary>
/// Only the Start/Finish session bookkeeping is covered here: CaptureNextStep drives real
/// SendInput wheel events (see ScrollSimulator), which would move the developer machine's actual
/// input state, so the scroll loop itself stays a manual-verification path per CLAUDE.md.
/// </summary>
public class ScrollingCaptureServiceTests
{
    [Fact]
    public void Finish_ReleasesSessionState()
    {
        var service = new ScrollingCaptureService(new StubScreenCaptureService());
        service.Start(new Int32Rect(0, 0, 40, 30));

        Assert.Equal(1, service.FrameCount);

        service.Finish();

        // The service is a DI singleton, so frames left referenced here would stay alive for the
        // rest of the app's lifetime. Dropping them also re-arms CaptureNextStep's empty-state
        // fallback, so a step taken after a finished session starts a fresh one instead of
        // appending to the previous session's frames.
        Assert.Equal(0, service.FrameCount);
    }

    [Fact]
    public void Finish_ReturnedImageSurvivesTheStateReset()
    {
        var service = new ScrollingCaptureService(new StubScreenCaptureService());
        service.Start(new Int32Rect(0, 0, 40, 30));

        var result = service.Finish();

        Assert.Equal(40, result.Image.PixelWidth);
        Assert.Equal(30, result.Image.PixelHeight);
        Assert.Equal(0, service.FrameCount);
    }

    [Fact]
    public void Start_AfterFinish_BeginsAFreshSession()
    {
        var service = new ScrollingCaptureService(new StubScreenCaptureService());
        service.Start(new Int32Rect(0, 0, 40, 30));
        service.Finish();

        service.Start(new Int32Rect(0, 0, 20, 10));
        var result = service.Finish();

        Assert.Equal(20, result.Image.PixelWidth);
        Assert.Equal(10, result.Image.PixelHeight);
    }

    private sealed class StubScreenCaptureService : IScreenCaptureService
    {
        public BitmapSource CaptureRect(Int32Rect rect)
        {
            var bitmap = new WriteableBitmap(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
