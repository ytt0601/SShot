using SShot.Core.Models;

namespace SShot.Core.Capture;

/// <summary>
/// Shared abstraction across all capture modes (full screen / region / window / scrolling).
/// Interactive modes (region, window picker) await user input before resolving; returns
/// null if the user cancels (e.g. Escape).
/// </summary>
public interface ICaptureService
{
    Task<CaptureResult?> CaptureAsync();
}
