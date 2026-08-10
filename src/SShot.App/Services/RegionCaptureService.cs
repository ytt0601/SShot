using System.Windows;
using System.Windows.Media.Imaging;
using SShot.App.Views;
using SShot.Core.Capture;
using SShot.Core.Capture.Dpi;
using SShot.Core.Models;

namespace SShot.App.Services;

/// <summary>
/// Orchestrates the region-selection overlay windows. Lives in SShot.App (not Core) because
/// it creates WPF Windows; Core intentionally has no Window/View types (see CLAUDE.md).
/// </summary>
public sealed class RegionCaptureService(IScreenCaptureService screenCapture) : ICaptureService
{
    public Task<CaptureResult?> CaptureAsync()
    {
        var virtualBounds = VirtualScreenBounds.GetVirtualDesktopBounds();
        var frozenDesktop = screenCapture.CaptureRect(virtualBounds);
        var monitors = VirtualScreenBounds.GetMonitors();

        var session = new RegionSelectionSession();
        var tcs = new TaskCompletionSource<CaptureResult?>();
        var overlays = new List<RegionSelectionOverlayWindow>();

        void Cleanup()
        {
            session.SelectionCompleted -= OnCompleted;
            session.Cancelled -= OnCancelled;
            foreach (var overlay in overlays)
            {
                overlay.Close();
            }
        }

        void OnCompleted(object? s, Rect physicalSelection)
        {
            Cleanup();

            if (physicalSelection.Width < 1 || physicalSelection.Height < 1)
            {
                tcs.TrySetResult(null);
                return;
            }

            var cropRect = new Int32Rect(
                (int)Math.Round(physicalSelection.X) - virtualBounds.X,
                (int)Math.Round(physicalSelection.Y) - virtualBounds.Y,
                (int)Math.Round(physicalSelection.Width),
                (int)Math.Round(physicalSelection.Height));

            cropRect = ClampToBitmap(cropRect, frozenDesktop);
            if (cropRect.Width < 1 || cropRect.Height < 1)
            {
                tcs.TrySetResult(null);
                return;
            }

            // The eager pixel copy below can throw under memory pressure for huge selections;
            // CompleteWith (see TaskCompletionSourceExtensions) guarantees the Task still completes.
            tcs.CompleteWith(() =>
            {
                // CroppedBitmap is a lazy view over its Source, not a pixel copy - Freeze() doesn't
                // materialize it, so keeping this around would keep the entire (potentially huge,
                // multi-monitor) frozenDesktop bitmap alive for as long as the result is referenced
                // (e.g. in CaptureHistoryService, for the app's whole session). WriteableBitmap's
                // BitmapSource constructor eagerly copies pixel data, fully decoupling the result
                // from frozenDesktop.
                var cropped = new WriteableBitmap(new CroppedBitmap(frozenDesktop, cropRect));
                cropped.Freeze();

                var absoluteBounds = new Int32Rect(
                    cropRect.X + virtualBounds.X, cropRect.Y + virtualBounds.Y,
                    cropRect.Width, cropRect.Height);

                return new CaptureResult(cropped, absoluteBounds);
            });
        }

        void OnCancelled(object? s, EventArgs e)
        {
            Cleanup();
            tcs.TrySetResult(null);
        }

        session.SelectionCompleted += OnCompleted;
        session.Cancelled += OnCancelled;

        foreach (var monitor in monitors)
        {
            var sliceRect = new Int32Rect(
                monitor.Bounds.X - virtualBounds.X, monitor.Bounds.Y - virtualBounds.Y,
                monitor.Bounds.Width, monitor.Bounds.Height);
            var slice = new CroppedBitmap(frozenDesktop, sliceRect);
            slice.Freeze();

            var overlay = new RegionSelectionOverlayWindow(monitor.Bounds, slice, session);
            overlays.Add(overlay);
            overlay.Show();
        }

        return tcs.Task;
    }

    private static Int32Rect ClampToBitmap(Int32Rect rect, BitmapSource bitmap)
    {
        int x = Math.Max(0, rect.X);
        int y = Math.Max(0, rect.Y);
        int width = Math.Min(rect.Width - (x - rect.X), bitmap.PixelWidth - x);
        int height = Math.Min(rect.Height - (y - rect.Y), bitmap.PixelHeight - y);
        return new Int32Rect(x, y, Math.Max(0, width), Math.Max(0, height));
    }
}
