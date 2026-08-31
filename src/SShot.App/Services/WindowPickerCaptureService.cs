using SShot.App.Views;
using SShot.Core.Capture;
using SShot.Core.Models;

namespace SShot.App.Services;

/// <summary>
/// Interactive "hover to highlight, click to confirm" window capture. Lives in SShot.App
/// (not Core) because it creates a WPF Window; Core has no Window/View types.
/// </summary>
public sealed class WindowPickerCaptureService(WindowCaptureService windowCapture) : ICaptureService
{
    public Task<CaptureResult?> CaptureAsync()
    {
        var tcs = new TaskCompletionSource<CaptureResult?>();
        var overlay = new WindowPickerOverlayWindow();
        bool confirmed = false;

        overlay.WindowConfirmed += (_, hwnd) =>
        {
            // Set before Close(), which synchronously raises Closed below - without the flag that
            // handler would win the race and complete the Task with "cancelled". Close() still
            // comes first so the overlay's highlight isn't on screen when CaptureWindow BitBlts.
            confirmed = true;
            overlay.Close();
            tcs.CompleteWith(() => windowCapture.CaptureWindow(hwnd));
        };
        overlay.Cancelled += (_, _) => overlay.Close();

        // Last resort: the overlay takes keyboard focus, so it can also be closed by routes that
        // raise neither event (Alt+F4). Without this the Task would never complete, and the caller
        // holding the CaptureGate scope would keep the primary window hidden forever.
        overlay.Closed += (_, _) =>
        {
            if (!confirmed)
            {
                tcs.TrySetResult(null);
            }
        };

        overlay.Show();
        return tcs.Task;
    }
}
