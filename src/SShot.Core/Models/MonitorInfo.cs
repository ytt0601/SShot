using System.Windows;

namespace SShot.Core.Models;

/// <summary>Bounds are in physical pixels, virtual-desktop-relative.</summary>
public sealed record MonitorInfo(Int32Rect Bounds, bool IsPrimary, string DeviceName);
