using System.Windows;

namespace SShot.Core.Annotation;

/// <summary>
/// Pure clamp/validate logic shared by the mouse-drag crop gesture and the coordinate-entry
/// crop dialog, so both apply identical bounds rules. Returns null when the clamped rectangle
/// would have no area (fully outside the image, or a zero/negative size after clamping).
/// </summary>
public static class CropGeometry
{
    public static Int32Rect? Clamp(int x, int y, int width, int height, int imageWidth, int imageHeight)
    {
        int clampedX = Math.Max(0, x);
        int clampedY = Math.Max(0, y);
        // Shrink width/height by whatever was clipped off the left/top, so a rect that starts
        // outside the image doesn't keep its full original width/height once its origin is
        // pulled back to 0 (that would bulge the result past the originally intended right/bottom edge).
        int clampedWidth = Math.Min(width - (clampedX - x), imageWidth - clampedX);
        int clampedHeight = Math.Min(height - (clampedY - y), imageHeight - clampedY);

        return clampedWidth < 1 || clampedHeight < 1
            ? null
            : new Int32Rect(clampedX, clampedY, clampedWidth, clampedHeight);
    }
}
