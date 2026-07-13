using SShot.Core.Interop;

namespace SShot.Core.Capture;

/// <summary>
/// Thin public wrapper over DwmFlush (internal to this assembly via DwmNativeMethods) so the App
/// layer can wait for the DWM compositor to finish presenting after hiding a window and before
/// capturing the screen - see the hide-then-capture helpers in MainWindow/FloatingToolbarWindow/
/// GlobalHotkeyManager.
/// </summary>
public static class DwmSync
{
    public static void WaitForNextFrame() => DwmNativeMethods.DwmFlush();
}
