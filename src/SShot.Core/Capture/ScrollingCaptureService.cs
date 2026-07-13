using System.Windows;
using System.Windows.Media.Imaging;
using SShot.Core.Models;

namespace SShot.Core.Capture;

/// <summary>
/// Step-based (not a single blocking loop) so the App-layer HUD can drive one step per timer
/// tick, giving the target app time to render between scrolls and letting the user watch
/// progress / hit Stop at any moment. Safety-capped at <see cref="MaxFrames"/> to avoid runaway
/// capture on an infinite-scroll page.
/// </summary>
public sealed class ScrollingCaptureService(IScreenCaptureService screenCapture)
{
    public const int MaxFrames = 100;

    private readonly ScrollSimulator _scrollSimulator = new();
    private readonly List<BitmapSource> _frames = [];

    public int FrameCount => _frames.Count;

    public bool ReachedSafetyLimit => _frames.Count >= MaxFrames;

    public void Start(Int32Rect windowBounds)
    {
        _frames.Clear();
        _frames.Add(screenCapture.CaptureRect(windowBounds));
    }

    /// <summary>
    /// Scrolls once and captures the resulting frame. Returns false when the new frame has no
    /// content beyond what the previous frame already showed - the caller should treat that as
    /// "reached the end" and stop automatically.
    /// </summary>
    public bool CaptureNextStep(Int32Rect windowBounds, int scrollNotches = 3)
    {
        if (_frames.Count == 0)
        {
            Start(windowBounds);
            return true;
        }

        var center = new Point(windowBounds.X + (windowBounds.Width / 2.0), windowBounds.Y + (windowBounds.Height / 2.0));
        _scrollSimulator.ScrollDown(center, scrollNotches);

        var newFrame = screenCapture.CaptureRect(windowBounds);
        bool hasNewContent = FrameStitcher.HasNewContent(_frames[^1], newFrame);
        _frames.Add(newFrame);
        return hasNewContent;
    }

    public CaptureResult Finish()
    {
        var stitched = FrameStitcher.Stitch(_frames);
        return new CaptureResult(stitched, new Int32Rect(0, 0, stitched.PixelWidth, stitched.PixelHeight));
    }
}
