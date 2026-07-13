using System.Runtime.InteropServices;
using System.Windows;
using SShot.Core.Interop;
using SShot.Core.Models;

namespace SShot.Core.Capture;

public sealed class WindowCaptureService(IScreenCaptureService screenCapture) : ICaptureService
{
    /// <summary>Non-interactive: captures whatever window currently has focus.</summary>
    public Task<CaptureResult?> CaptureAsync()
    {
        return Task.FromResult(CaptureWindow(User32.GetForegroundWindow()));
    }

    public CaptureResult? CaptureWindow(IntPtr hwnd)
    {
        var bounds = TryGetWindowBounds(hwnd);
        if (bounds is null)
        {
            return null;
        }

        var image = screenCapture.CaptureRect(bounds.Value);
        return new CaptureResult(image, bounds.Value);
    }

    /// <summary>
    /// Uses DWMWA_EXTENDED_FRAME_BOUNDS rather than GetWindowRect for top-level windows:
    /// on Windows 10/11, GetWindowRect includes an invisible resize-border margin that
    /// would otherwise bleed a sliver of desktop into the captured image's edges. Child
    /// windows (controls/panes picked via the window picker's Ctrl mode) aren't top-level
    /// frames DWM tracks that way, so GetWindowRect is used for them instead.
    /// </summary>
    public static Int32Rect? TryGetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        bool isTopLevel = User32.GetAncestor(hwnd, User32.GA_ROOT) == hwnd;

        User32.RECT rect;
        if (isTopLevel)
        {
            int hr = DwmNativeMethods.DwmGetWindowAttribute(
                hwnd, DwmNativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<User32.RECT>());
            if (hr != 0)
            {
                return null;
            }
        }
        else if (!User32.GetWindowRect(hwnd, out rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return new Int32Rect(rect.Left, rect.Top, width, height);
    }
}
