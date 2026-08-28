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

    // A capture that shows nothing new is not proof the document ended: apps with smooth-scroll
    // animation (Windows 11 Notepad settles in roughly 600ms) are still repainting when the step
    // fires, and a mid-animation frame lands on no integer row offset, so the overlap search
    // scores a full-height overlap and looks exactly like "reached the end". Only stop after this
    // many consecutive steps report no progress.
    private const int MaxStepsWithoutProgress = 3;

    // Fractions of the frame height excluded from the overlap search. A window capture spans the
    // whole window, so it carries chrome that never scrolls - Windows 11 Notepad alone contributes
    // a tab strip and toolbar on top and a status bar below. At the true overlap those rows line
    // up against scrolled content and score as a mismatch, which was enough to push the correct
    // candidate past FindOverlap's acceptance threshold and have it report "no reliable overlap"
    // for an ordinary text window.
    private const double ChromeTopFraction = 0.12;
    private const double ChromeBottomFraction = 0.08;

    private readonly ScrollSimulator _scrollSimulator = new();
    private readonly List<BitmapSource> _frames = [];

    // Each frame's BGRA extraction and each pair's overlap search are expensive (full-frame
    // pixel copy / O(height) candidate scan), so both are done exactly once per frame here:
    // the last frame's pixels are kept for the next step's comparison, and the overlaps are
    // fed to Stitch at Finish instead of being recomputed from scratch.
    private readonly List<int> _overlaps = [];
    private byte[] _lastFramePixels = [];
    private int _lastFrameStride;

    // Set once a wheel event has been synthesized whose resulting frame has not been captured
    // yet, so the next step knows there is something new to capture before scrolling again.
    private bool _awaitingRender;

    // Derived from the first frame's height in Start, so the overlap search and the stitch agree
    // on where the window's non-scrolling chrome sits.
    private int _chromeTopRows;
    private int _chromeBottomRows;

    // Consecutive steps whose capture showed nothing beyond the last committed frame. A single
    // such step does not mean the end of the document - see the remarks on CaptureNextStep.
    private int _stepsWithoutProgress;

    public int FrameCount => _frames.Count;

    public bool ReachedSafetyLimit => _frames.Count >= MaxFrames;

    public void Start(Int32Rect windowBounds)
    {
        Reset();
        var firstFrame = screenCapture.CaptureRect(windowBounds);
        _frames.Add(firstFrame);
        (_lastFramePixels, _lastFrameStride) = FrameStitcher.ToBgra32Pixels(firstFrame);
        _chromeTopRows = (int)(firstFrame.PixelHeight * ChromeTopFraction);
        _chromeBottomRows = (int)(firstFrame.PixelHeight * ChromeBottomFraction);
    }

    /// <summary>
    /// Advances the session by one step: captures the frame the previous step's scroll produced,
    /// then scrolls again. Returns false once <see cref="MaxStepsWithoutProgress"/> consecutive
    /// steps have shown nothing beyond the last committed frame - the caller should treat that as
    /// "reached the end" and stop automatically. The first step after <see cref="Start"/> only
    /// scrolls, so it always reports new content.
    /// </summary>
    /// <remarks>
    /// The scroll and the capture that observes it are deliberately in different steps.
    /// ScrollSimulator synthesizes the wheel event through SendInput and the target app handles
    /// WM_MOUSEWHEEL and repaints on its own thread, so a capture taken immediately after the
    /// scroll still shows the pre-scroll content: every frame would match its predecessor and the
    /// very first step would report "no new content", ending the session with a single frame.
    /// Splitting them lets the caller's tick interval double as the render delay without blocking
    /// here.
    /// </remarks>
    public bool CaptureNextStep(Int32Rect windowBounds, int scrollNotches = 3)
    {
        if (_frames.Count == 0)
        {
            Start(windowBounds);
        }

        if (!_awaitingRender)
        {
            ScrollOnce(windowBounds, scrollNotches);
            return true;
        }

        var newFrame = screenCapture.CaptureRect(windowBounds);
        var previousFrame = _frames[^1];
        var (newPixels, newStride) = FrameStitcher.ToBgra32Pixels(newFrame);
        int compareHeight = Math.Min(previousFrame.PixelHeight, newFrame.PixelHeight);
        int compareWidth = Math.Min(previousFrame.PixelWidth, newFrame.PixelWidth);
        // A later frame can be shorter than the first (the window was resized mid-capture), so the
        // margins are clamped to leave a band worth comparing rather than swallowing the frame.
        int ignoreTop = Math.Min(_chromeTopRows, compareHeight / 4);
        int ignoreBottom = Math.Min(_chromeBottomRows, compareHeight / 4);
        int overlap = FrameStitcher.FindOverlap(
            _lastFramePixels, newPixels, compareWidth, compareHeight, _lastFrameStride, newStride,
            ignoreTopRows: ignoreTop, ignoreBottomRows: ignoreBottom);

        if (overlap < compareHeight)
        {
            _overlaps.Add(overlap);
            _frames.Add(newFrame);
            _lastFramePixels = newPixels;
            _lastFrameStride = newStride;
            _stepsWithoutProgress = 0;
            ScrollOnce(windowBounds, scrollNotches);
            return true;
        }

        // The frame showed nothing new. Discard it rather than committing a frame that adds no
        // rows, and give the target another step before concluding the document ended - without
        // scrolling again, so the pending scroll keeps settling.
        _stepsWithoutProgress++;
        if (_stepsWithoutProgress < MaxStepsWithoutProgress)
        {
            return true;
        }

        _awaitingRender = false;
        return false;
    }

    /// <summary>
    /// Stitches the captured frames and ends the session. This service is a DI singleton, so the
    /// session state is released here rather than only on the next <see cref="Start"/>: otherwise
    /// up to <see cref="MaxFrames"/> full-size frames plus the last frame's BGRA buffer would stay
    /// alive for the rest of the app's lifetime, even after the user discarded the result.
    /// Releasing here also re-arms <see cref="CaptureNextStep"/>'s empty-state fallback, so a step
    /// taken after a finished session starts a fresh one instead of appending to the old frames.
    /// </summary>
    public CaptureResult Finish()
    {
        try
        {
            var stitched = FrameStitcher.Stitch(_frames, _overlaps, _chromeTopRows, _chromeBottomRows);
            return new CaptureResult(stitched, new Int32Rect(0, 0, stitched.PixelWidth, stitched.PixelHeight));
        }
        finally
        {
            Reset();
        }
    }

    private void ScrollOnce(Int32Rect windowBounds, int scrollNotches)
    {
        var center = new Point(
            windowBounds.X + (windowBounds.Width / 2.0), windowBounds.Y + (windowBounds.Height / 2.0));
        _scrollSimulator.ScrollDown(center, scrollNotches);
        _awaitingRender = true;
    }

    private void Reset()
    {
        _frames.Clear();
        _overlaps.Clear();
        _lastFramePixels = [];
        _lastFrameStride = 0;
        _awaitingRender = false;
        _stepsWithoutProgress = 0;
        _chromeTopRows = 0;
        _chromeBottomRows = 0;
    }
}
