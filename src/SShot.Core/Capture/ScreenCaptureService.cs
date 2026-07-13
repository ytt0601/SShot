using System.Windows;
using System.Windows.Media.Imaging;
using SShot.Core.Imaging;
using SShot.Core.Interop;

namespace SShot.Core.Capture;

/// <summary>
/// BitBlt(SRCCOPY | CAPTUREBLT)-based screen capture. CAPTUREBLT ensures layered/transparent
/// windows (e.g. taskbar blur) are included. Chosen over Windows.Graphics.Capture (WGC) because
/// WGC shows visible capture chrome and a session-start delay, which is wrong for an instant
/// screenshot tool. See CLAUDE.md for the full rationale.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    public BitmapSource CaptureRect(Int32Rect rect)
    {
        IntPtr screenDc = User32.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get a device context for the screen.");
        }

        IntPtr memDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create a memory device context for the capture.");
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, rect.Width, rect.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create a compatible bitmap for the capture.");
            }

            oldBitmap = NativeMethods.SelectObject(memDc, bitmap);
            if (oldBitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to select the capture bitmap into the memory device context.");
            }

            bool blitSucceeded = NativeMethods.BitBlt(
                memDc, 0, 0, rect.Width, rect.Height,
                screenDc, rect.X, rect.Y,
                NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
            if (!blitSucceeded)
            {
                throw new InvalidOperationException("BitBlt failed to copy the screen into the capture bitmap.");
            }

            return BitmapConverters.FromHBitmap(bitmap);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memDc, oldBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memDc);
            }

            User32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
