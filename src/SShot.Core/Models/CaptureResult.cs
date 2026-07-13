using System.Windows;
using System.Windows.Media.Imaging;

namespace SShot.Core.Models;

/// <summary>PhysicalPixelBounds is virtual-desktop-relative, in physical pixels.</summary>
public sealed record CaptureResult(BitmapSource Image, Int32Rect PhysicalPixelBounds);
