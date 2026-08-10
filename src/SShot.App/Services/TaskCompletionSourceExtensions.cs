namespace SShot.App.Services;

/// <summary>
/// Shared completion helper for the capture services' <see cref="TaskCompletionSource{T}"/>-based
/// callback flow (region/window/scrolling capture): every caller of <see cref="ICaptureService.CaptureAsync"/>
/// holds the CaptureGate scope and keeps the main window hidden until the returned Task completes,
/// so a result-producing callback must never let an exception escape without completing it.
/// </summary>
internal static class TaskCompletionSourceExtensions
{
    /// <summary>Runs <paramref name="produce"/> and completes <paramref name="tcs"/> with its
    /// result, or with the thrown exception if it fails.</summary>
    public static void CompleteWith<T>(this TaskCompletionSource<T> tcs, Func<T> produce)
    {
        try
        {
            tcs.TrySetResult(produce());
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }
}
