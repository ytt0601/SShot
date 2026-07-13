using System.Windows;
using SShot.Core.Interop;

namespace SShot.Core.Capture;

public sealed class ScrollSimulator
{
    /// <summary>Moves the cursor over the given screen point and synthesizes a downward scroll.</summary>
    public void ScrollDown(Point screenPoint, int notches = 3)
    {
        InputNativeMethods.MoveCursorTo((int)screenPoint.X, (int)screenPoint.Y);
        InputNativeMethods.ScrollWheel(-Math.Abs(notches));
    }
}
