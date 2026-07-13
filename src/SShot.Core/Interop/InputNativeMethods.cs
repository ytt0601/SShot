using System.Runtime.InteropServices;

namespace SShot.Core.Interop;

/// <summary>
/// SendInput-based wheel event synthesis for scrolling capture. Chosen over posting
/// WM_VSCROLL/WM_MOUSEWHEEL directly to the target HWND: direct message posting works for
/// classic Win32 scrollable controls but is unreliable for Chromium/UWP/WinUI apps that
/// implement their own scrolling independent of the standard scrollbar protocol. A synthesized
/// real input event is indistinguishable from a user action and works far more universally.
/// </summary>
internal static class InputNativeMethods
{
    private const int InputMouse = 0;
    private const uint MouseEventFWheel = 0x0800;
    private const int WheelDelta = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int Type;
        public MOUSEINPUT Mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    internal static void MoveCursorTo(int x, int y) => SetCursorPos(x, y);

    /// <summary>Positive notches scroll up (away from user), negative scroll down.</summary>
    internal static void ScrollWheel(int notches)
    {
        var input = new INPUT
        {
            Type = InputMouse,
            Mi = new MOUSEINPUT
            {
                DwFlags = MouseEventFWheel,
                MouseData = unchecked((uint)(notches * WheelDelta)),
            },
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
