using System.Windows;
using SShot.Core.Interop;

namespace SShot.Core.Capture;

/// <summary>
/// Small public facade over the Win32 calls the window-picker overlay needs. Keeps all
/// P/Invoke declarations internal to Core's Interop layer while still letting the App-layer
/// overlay window (which can't see internal types across the assembly boundary) drive them.
/// </summary>
public static class WindowPickerSupport
{
    /// <summary>Top-level window (walked up via GA_ROOT) currently under the cursor, or Zero.</summary>
    public static IntPtr GetRootWindowAtCursor()
    {
        IntPtr hit = GetWindowAtCursor();
        return hit == IntPtr.Zero ? IntPtr.Zero : User32.GetAncestor(hit, User32.GA_ROOT);
    }

    /// <summary>The exact window under the cursor (no GA_ROOT walk), which may be a child
    /// control/pane rather than a top-level window. Used by the picker's Ctrl mode to let
    /// users target a sub-window inside an app instead of always the whole app window.</summary>
    public static IntPtr GetWindowAtCursor()
    {
        User32.GetCursorPos(out var cursor);
        return User32.WindowFromPoint(cursor);
    }

    public static bool IsLeftMouseButtonDown() => (User32.GetAsyncKeyState(User32.VK_LBUTTON) & 0x8000) != 0;

    public static bool IsCtrlKeyDown() => (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;

    /// <summary>Polled rather than read from a key event because the picker overlay is
    /// click-through: a click falls through to the app underneath, which takes the foreground,
    /// so the overlay can lose keyboard focus and never see an Escape key event again.</summary>
    public static bool IsEscapeKeyDown() => (User32.GetAsyncKeyState(User32.VK_ESCAPE) & 0x8000) != 0;

    /// <summary>Makes a window click-through (WS_EX_TRANSPARENT | WS_EX_LAYERED) so it can draw
    /// a hover highlight on top of the desktop without intercepting mouse hit-testing.</summary>
    public static void MakeClickThrough(IntPtr hwnd)
    {
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_TRANSPARENT | User32.WS_EX_LAYERED);
    }

    /// <summary>Pins a window to exact physical-pixel bounds, bypassing WPF's DIP-based
    /// Left/Top/Width/Height. Needed for overlays spanning the virtual desktop: WPF converts
    /// DIPs with the single DPI it assigned the window, which on mixed-DPI setups can differ
    /// from the scale the DIPs were computed with, landing the window off-target.</summary>
    public static void SetPhysicalBounds(IntPtr hwnd, Int32Rect physicalBounds)
    {
        User32.SetWindowPos(
            hwnd, IntPtr.Zero,
            physicalBounds.X, physicalBounds.Y, physicalBounds.Width, physicalBounds.Height,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
    }
}
