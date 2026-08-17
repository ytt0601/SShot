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
                hud.Close();
                _isCapturing = false;
            }

            void Finish()
            {
                if (finishing)
                {
                    return;
                }

                Teardown();
                tcs.CompleteWith(scrollingCapture.Finish);
            }

            void Fail(Exception ex)
            {
                if (finishing)
                {
                    return;
                }

                Teardown();
                tcs.TrySetException(ex);
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

    private static void PositionHudNearWindow(Window hud, Int32Rect targetBounds)
    {
        var hudPhysicalBounds = new Int32Rect(
            targetBounds.X + targetBounds.Width - 300, targetBounds.Y + 20, 280, 130);
        double scale = DpiHelper.GetDpiScaleForMonitor(hudPhysicalBounds);
        var dip = DpiHelper.PhysicalToDip(hudPhysicalBounds, scale);
        hud.Left = dip.X;
        hud.Top = dip.Y;
    }
}
