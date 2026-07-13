using System.Runtime.InteropServices;

namespace SShot.Core.Interop;

/// <summary>
/// Shcore.dll P/Invoke signature for per-monitor DPI lookup, used to position
/// not-yet-shown overlay windows correctly on a specific monitor before Show().
/// </summary>
internal static class Shcore
{
    internal enum MonitorDpiType
    {
        EffectiveDpi = 0,
        AngularDpi = 1,
        RawDpi = 2,
    }

    [DllImport("Shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
}
