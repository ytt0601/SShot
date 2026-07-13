using System.Runtime.InteropServices;

namespace SShot.Core.Interop;

internal static class DwmNativeMethods
{
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out User32.RECT pvAttribute, int cbAttribute);

    /// <summary>
    /// Blocks until the DWM has finished composing and presenting the current frame. Called
    /// after hiding the app's own window and before capturing the screen, so the just-hidden
    /// window can't still be part of the next composited frame BitBlt reads (composition is
    /// vsync-throttled and asynchronous relative to ShowWindow(SW_HIDE) returning).
    /// </summary>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmFlush();
}
