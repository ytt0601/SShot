using System.Runtime.InteropServices;
using System.Windows;
using SShot.Core.Interop;
using SShot.Core.Models;

namespace SShot.Core.Capture.Dpi;

/// <summary>
/// Single source of truth for the virtual desktop and per-monitor bounds, always in
/// physical pixels. All capture services must resolve coordinates through this class
/// rather than re-deriving monitor/virtual-desktop geometry themselves.
/// </summary>
public static class VirtualScreenBounds
{
    public static Int32Rect GetVirtualDesktopBounds()
    {
        int x = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
        int y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
        int width = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN);
        int height = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN);
        return new Int32Rect(x, y, width, height);
    }

    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref User32.RECT rect, IntPtr data)
        {
            var info = new User32.MONITORINFOEX { cbSize = Marshal.SizeOf<User32.MONITORINFOEX>() };
            if (User32.GetMonitorInfo(hMonitor, ref info))
            {
                var bounds = new Int32Rect(
                    info.rcMonitor.Left,
                    info.rcMonitor.Top,
                    info.rcMonitor.Right - info.rcMonitor.Left,
                    info.rcMonitor.Bottom - info.rcMonitor.Top);
                bool isPrimary = (info.dwFlags & User32.MONITORINFOF_PRIMARY) != 0;
                monitors.Add(new MonitorInfo(bounds, isPrimary, info.szDevice));
            }

            return true;
        }

        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return monitors;
    }
}
