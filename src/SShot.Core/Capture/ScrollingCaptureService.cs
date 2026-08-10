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

    // Each frame's BGRA extraction and each pair's overlap search are expensive (full-frame
    // pixel copy / O(height) candidate scan), so both are done exactly once per frame here:
    // the last frame's pixels are kept for the next step's comparison, and the overlaps are
    // fed to Stitch at Finish instead of being recomputed from scratch.
    private readonly List<int> _overlaps = [];
    private byte[] _lastFramePixels = [];
    private int _lastFrameStride;

    public int FrameCount => _frames.Count;

    public bool ReachedSafetyLimit => _frames.Count >= MaxFrames;

    public void Start(Int32Rect windowBounds)
    {
        _frames.Clear();
        _overlaps.Clear();
        var firstFrame = screenCapture.CaptureRect(windowBounds);
        _frames.Add(firstFrame);
        (_lastFramePixels, _lastFrameStride) = FrameStitcher.ToBgra32Pixels(firstFrame);
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
        var previousFrame = _frames[^1];
        var (newPixels, newStride) = FrameStitcher.ToBgra32Pixels(newFrame);
        int compareHeight = Math.Min(previousFrame.PixelHeight, newFrame.PixelHeight);
        int compareWidth = Math.Min(previousFrame.PixelWidth, newFrame.PixelWidth);
        int overlap = FrameStitcher.FindOverlap(
            _lastFramePixels, newPixels, compareWidth, compareHeight, _lastFrameStride, newStride);

        _overlaps.Add(overlap);
        _frames.Add(newFrame);
        _lastFramePixels = newPixels;
        _lastFrameStride = newStride;

        return overlap < compareHeight;
    }

    public CaptureResult Finish()
    {
        var stitched = FrameStitcher.Stitch(_frames, _overlaps);
        return new CaptureResult(stitched, new Int32Rect(0, 0, stitched.PixelWidth, stitched.PixelHeight));
    }
}
