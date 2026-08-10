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

        // A minimized window still reports S_OK extended frame bounds, but they sit at the
        // off-screen minimize position (~-32000) - BitBlt from there always yields garbage. This
        // is deliberately a blanket rule for every caller of TryGetWindowBounds (plain window
        // capture, the window-picker's hover highlight, and scrolling capture's target-still-
        // present check), not just the scrolling-capture case that originally surfaced it: a
        // minimized window's bounds are unusable for any of them, so returning null here - "no
        // capturable bounds" - is correct regardless of which caller asked.
        if (User32.IsIconic(hwnd))
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
