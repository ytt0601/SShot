using SShot.App.Views;
using SShot.Core.Capture;
using SShot.Core.Models;

namespace SShot.App.Services;

/// <summary>
/// Interactive "hover to highlight, click to confirm" window capture. Lives in SShot.App
/// (not Core) because it creates a WPF Window; Core has no Window/View types (see CLAUDE.md).
/// </summary>
public sealed class WindowPickerCaptureService(WindowCaptureService windowCapture) : ICaptureService
{
    public Task<CaptureResult?> CaptureAsync()
    {
        var tcs = new TaskCompletionSource<CaptureResult?>();
        var overlay = new WindowPickerOverlayWindow();

        overlay.WindowConfirmed += (_, hwnd) =>
        {
            overlay.Close();
            tcs.TrySetResult(windowCapture.CaptureWindow(hwnd));
        };
        overlay.Cancelled += (_, _) =>
        {
            overlay.Close();
            tcs.TrySetResult(null);
        };

        overlay.Show();
        return tcs.Task;
    }
}
