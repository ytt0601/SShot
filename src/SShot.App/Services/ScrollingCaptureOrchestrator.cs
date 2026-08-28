using System.Windows;
using System.Windows.Threading;
using SShot.App.Views;
using SShot.Core.Capture;
using SShot.Core.Capture.Dpi;
using SShot.Core.Models;

namespace SShot.App.Services;

/// <summary>
/// Reuses the window picker overlay to choose a target, then drives the scroll-and-capture
/// loop from a DispatcherTimer (one step per tick) so the target app has time to render between
/// scrolls and the user can watch progress / hit Stop at any moment via the HUD. Lives in
/// SShot.App (not Core) because it creates WPF Windows.
/// </summary>
public sealed class ScrollingCaptureOrchestrator(ScrollingCaptureService scrollingCapture) : ICaptureService
{
    private const int StepIntervalMs = 400;

    // Matches ScrollCaptureHudWindow.xaml's Width/Height (DIP), plus the gap kept between the HUD
    // and the captured window.
    private const int HudDipWidth = 280;
    private const int HudDipHeight = 130;
    private const int HudDipGap = 12;

    // ScrollingCaptureService is a DI singleton holding mutable session state (_frames), so two
    // overlapping scroll-capture sessions (e.g. the hotkey pressed twice in quick succession)
    // would otherwise let the second Start() clear frames the first session's timer is still
    // appending to. Guard against re-entry instead of relying on caller discipline.
    private bool _isCapturing;

    public Task<CaptureResult?> CaptureAsync()
    {
        if (_isCapturing)
        {
            return Task.FromResult<CaptureResult?>(null);
        }

        _isCapturing = true;
        var tcs = new TaskCompletionSource<CaptureResult?>();
        var picker = new WindowPickerOverlayWindow();
        bool confirmed = false;

        picker.WindowConfirmed += (_, hwnd) =>
        {
            // Set before Close(), which synchronously raises Closed below - without the flag that
            // handler would win the race and complete the Task with "cancelled".
            confirmed = true;
            picker.Close();
            StartScrollLoop(hwnd, tcs);
        };
        picker.Cancelled += (_, _) => picker.Close();

        // Last resort: the picker takes keyboard focus, so it can also be closed by routes that
        // raise neither event (Alt+F4). Without this the Task would never complete, and the caller
        // holding the CaptureGate scope would keep the primary window hidden forever.
        picker.Closed += (_, _) =>
        {
            if (!confirmed)
            {
                _isCapturing = false;
                tcs.TrySetResult(null);
            }
        };

        picker.Show();
        return tcs.Task;
    }

    private void StartScrollLoop(IntPtr targetHwnd, TaskCompletionSource<CaptureResult?> tcs)
    {
        var bounds = WindowCaptureService.TryGetWindowBounds(targetHwnd);
        if (bounds is null)
        {
            _isCapturing = false;
            tcs.TrySetResult(null);
            return;
        }

        // The returned Task must always complete (success or failure): callers hold the
        // CaptureGate scope and keep the main window hidden until it does. Everything from
        // scrollingCapture.Start() through timer.Start() is inside one try for that reason - the
        // HUD's own construction can fail too (a XamlParseException from an unresolvable
        // {x:Static Strings.X} under a mis-published satellite assembly, say), and an escape there
        // would strand the Task with no window on screen and the capture gate held for the rest
        // of the session. An exception escaping the Tick handler would additionally leave the
        // timer running, repeating the failure every interval.
        ScrollCaptureHudWindow? pendingHud = null;
        DispatcherTimer? pendingTimer = null;

        try
        {
            scrollingCapture.Start(bounds.Value);

            var hud = new ScrollCaptureHudWindow();
            pendingHud = hud;
            PositionHudNearWindow(hud, bounds.Value);
            hud.UpdateFrameCount(scrollingCapture.FrameCount);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(StepIntervalMs) };
            pendingTimer = timer;
            bool finishing = false;

            void Teardown()
            {
                finishing = true;
                timer.Stop();
                _isCapturing = false;

                // Skipped when the HUD is already closing under its own steam (title-bar close
                // button or Alt+F4, both of which arrive here through its OnClosing): calling
                // Close() again from inside that close throws as the outer close unwinds.
                if (!hud.IsClosing)
                {
                    hud.Close();
                }
            }

            void Finish()
            {
                if (finishing)
                {
                    return;
                }

                // try/finally rather than a plain sequence: the caller holds the CaptureGate
                // scope and keeps the primary window hidden until this Task completes, so a throw
                // anywhere inside Teardown must not be able to strand it.
                try
                {
                    Teardown();
                }
                finally
                {
                    tcs.CompleteWith(scrollingCapture.Finish);
                }
            }

            void Fail(Exception ex)
            {
                if (finishing)
                {
                    return;
                }

                try
                {
                    Teardown();
                }
                finally
                {
                    tcs.TrySetException(ex);
                }
            }

            hud.StopRequested += (_, _) => Finish();

            timer.Tick += (_, _) =>
            {
                try
                {
                    var currentBounds = WindowCaptureService.TryGetWindowBounds(targetHwnd);
                    if (currentBounds is null)
                    {
                        Finish();
                        return;
                    }

                    bool hasNewContent = scrollingCapture.CaptureNextStep(currentBounds.Value);
                    hud.UpdateFrameCount(scrollingCapture.FrameCount);

                    if (!hasNewContent || scrollingCapture.ReachedSafetyLimit)
                    {
                        Finish();
                    }
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            };

            hud.Show();
            timer.Start();
        }
        catch (Exception ex)
        {
            // Reached only before the loop is armed (Finish/Fail own every path after that, and
            // both TrySet* calls are no-ops once one of them has completed the Task).
            pendingTimer?.Stop();
            pendingHud?.Close();
            _isCapturing = false;
            tcs.TrySetException(ex);
        }
    }

    /// <summary>
    /// Places the HUD outside the captured window whenever the desktop has room for it. Sitting
    /// on top of the target, the HUD is baked into every captured frame at the same spot and never
    /// scrolls, which does more than repeat it down the stitched image: its rows only line up on
    /// the "nothing scrolled at all" hypothesis, so it drags the true overlap's score up and the
    /// full-overlap score down, and FrameStitcher's search can pick the latter - reading as
    /// "reached the end" on the very first step.
    /// </summary>
    private static void PositionHudNearWindow(Window hud, Int32Rect targetBounds)
    {
        double targetScale = DpiHelper.GetDpiScaleForMonitor(targetBounds);
        int width = (int)Math.Ceiling(HudDipWidth * targetScale);
        int height = (int)Math.Ceiling(HudDipHeight * targetScale);
        int gap = (int)Math.Ceiling(HudDipGap * targetScale);

        var desktop = VirtualScreenBounds.GetVirtualDesktopBounds();
        Int32Rect[] candidates =
        [
            new(targetBounds.X + targetBounds.Width + gap, targetBounds.Y, width, height),
            new(targetBounds.X - gap - width, targetBounds.Y, width, height),
            new(targetBounds.X, targetBounds.Y + targetBounds.Height + gap, width, height),
            new(targetBounds.X, targetBounds.Y - gap - height, width, height),
        ];

        // A target that covers the whole virtual desktop leaves nowhere outside it, and a HUD
        // pushed off-screen would take the Stop button with it - so fall back to the old spot
        // inside the target rather than to something the user cannot reach.
        var placement = new Int32Rect(
            targetBounds.X + targetBounds.Width - width - gap, targetBounds.Y + gap, width, height);

        foreach (var candidate in candidates)
        {
            if (IsFullyInside(desktop, candidate))
            {
                placement = candidate;
                break;
            }
        }

        double scale = DpiHelper.GetDpiScaleForMonitor(placement);
        var dip = DpiHelper.PhysicalToDip(placement, scale);
        hud.Left = dip.X;
        hud.Top = dip.Y;
    }

    private static bool IsFullyInside(Int32Rect outer, Int32Rect inner) =>
        inner.X >= outer.X
        && inner.Y >= outer.Y
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Y + inner.Height <= outer.Y + outer.Height;
}
