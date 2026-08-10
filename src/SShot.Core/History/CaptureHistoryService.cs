using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SShot.Core.History;

/// <summary>
/// Capture history, newest first, capped at <paramref name="capacity"/>. Backed by PNG files under
/// <paramref name="folderPath"/> (an app-internal cache, not the user's SaveFolder) so the gallery
/// survives an app restart; the cap still applies to how many entries stay in the gallery, but
/// eviction now also deletes the cache file - safe because these files were never a user-initiated
/// save (see ImageFileService for that path).
/// </summary>
public sealed class CaptureHistoryService
{
    private const string TimestampFormat = "yyyyMMdd_HHmmss_fff";

    private readonly string _folderPath;
    private readonly int _capacity;

    public CaptureHistoryService(int capacity = 20)
        : this(DefaultFolderPath, capacity)
    {
    }

    internal CaptureHistoryService(string folderPath, int capacity = 20)
    {
        _folderPath = folderPath;
        _capacity = capacity;
        Items = [];
    }

    internal static string DefaultFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SShot", "History");

    public ObservableCollection<CaptureHistoryItem> Items { get; }

    public CaptureHistoryItem Add(BitmapSource image)
    {
        var capturedAt = DateTime.Now;
        string? filePath = SaveToDisk(image, capturedAt);
        var item = new CaptureHistoryItem(Guid.NewGuid(), image, CreateThumbnail(image, 160), capturedAt, filePath);
        Items.Insert(0, item);
        TrimToCapacity();
        return item;
    }

    /// <summary>
    /// Loads the persisted captures from the previous session. Not done in the constructor: the
    /// service is a DI singleton resolved during startup, and decoding up to <c>capacity</c> PNGs
    /// synchronously there would block the first window from showing. Call once after the primary
    /// window is up; decoding runs on a thread-pool thread (the results are frozen) and the items
    /// join <see cref="Items"/> on the calling context. Captures taken before this completes stay
    /// newest-first because everything loaded here is older than the session.
    /// </summary>
    public async Task LoadFromDiskAsync()
    {
        List<CaptureHistoryItem> loaded;
        try
        {
            loaded = await Task.Run(LoadFromDisk);
        }
        catch (Exception ex)
        {
            // Nothing observes this method's Task (it's fired-and-forgotten right after Show()),
            // so any exception left uncaught here - not just the disk-access ones LoadFromDisk's
            // own helpers already handle - would otherwise silently fault the Task and leave the
            // gallery empty for the whole session with no diagnostic trail.
            System.Diagnostics.Trace.TraceWarning($"CaptureHistoryService: failed to load history: {ex.Message}");
            return;
        }

        // A capture taken between Show() and this load's completion has already written its own
        // cache file, which the enumeration above may have picked up - skip anything whose file
        // is already represented in the gallery so it doesn't appear twice.
        var existingPaths = Items
            .Where(i => i.FilePath is not null)
            .Select(i => i.FilePath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in loaded)
        {
            if (item.FilePath is not null && existingPaths.Contains(item.FilePath))
            {
                continue;
            }

            // Items.Add() below already keeps the collection at or under capacity by construction,
            // so once session captures taken while this load was in flight have filled it, every
            // further older item here would just be added and immediately evicted-and-deleted by
            // TrimToCapacity - delete it directly instead of doing that pointless round trip
            // through the gallery (which would briefly surface, then remove, a capture the user
            // never asked to see).
            if (Items.Count >= _capacity)
            {
                if (item.FilePath is not null)
                {
                    DeleteFile(item.FilePath);
                }

                continue;
            }

            Items.Add(item);
        }
    }

    private void TrimToCapacity()
    {
        while (Items.Count > _capacity)
        {
            var evicted = Items[^1];
            Items.RemoveAt(Items.Count - 1);
            if (evicted.FilePath is not null)
            {
                DeleteFile(evicted.FilePath);
            }
        }
    }

    private List<CaptureHistoryItem> LoadFromDisk()
    {
        if (!Directory.Exists(_folderPath))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(_folderPath, "*.png")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        // Keep disk usage bounded by the same cap the in-memory gallery enforces, in case files
        // accumulated beyond it (e.g. the app was killed before an eviction could run).
        foreach (string staleFile in files.Skip(_capacity))
        {
            DeleteFile(staleFile);
        }

        var items = new List<CaptureHistoryItem>();
        foreach (string filePath in files.Take(_capacity))
        {
            var item = TryLoad(filePath);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static CaptureHistoryItem? TryLoad(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            DateTime capturedAt = ParseTimestamp(Path.GetFileNameWithoutExtension(filePath)) ?? File.GetLastWriteTime(filePath);
            return new CaptureHistoryItem(Guid.NewGuid(), bitmap, CreateThumbnail(bitmap, 160), capturedAt, filePath);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException)
        {
            // Corrupt or unreadable cache file - skip it rather than failing the whole gallery load.
            return null;
        }
    }

    private string? SaveToDisk(BitmapSource image, DateTime capturedAt)
    {
        try
        {
            Directory.CreateDirectory(_folderPath);
            string fileName = $"{capturedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture)}_{Guid.NewGuid():N}.png";
            string fullPath = Path.Combine(_folderPath, fileName);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);

            return fullPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked (AV scanner) or inaccessible (disk full, permissions) - degrade to an
            // in-memory-only history entry rather than crashing the capture that triggered this,
            // matching TryLoad/DeleteFile's handling of the same failure modes in this class.
            System.Diagnostics.Trace.TraceWarning($"CaptureHistoryService: failed to save capture to disk: {ex.Message}");
            return null;
        }
    }

    private static void DeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked (e.g. an AV scanner) or permission-denied - skip rather than fail the whole
            // eviction/cleanup pass. Traced (not silently swallowed) so a growing History folder
            // can be diagnosed; there's no app-wide logging framework, so Trace is the lightest
            // way to surface this without adding a dependency.
            System.Diagnostics.Trace.TraceWarning($"CaptureHistoryService: failed to delete '{filePath}': {ex.Message}");
        }
    }

    private static DateTime? ParseTimestamp(string fileNameWithoutExtension)
    {
        string prefix = fileNameWithoutExtension.Length >= TimestampFormat.Length
            ? fileNameWithoutExtension[..TimestampFormat.Length]
            : fileNameWithoutExtension;

        return DateTime.TryParseExact(prefix, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
    }

    private static BitmapSource CreateThumbnail(BitmapSource source, int maxDimension)
    {
        double scale = Math.Min(1.0, (double)maxDimension / Math.Max(source.PixelWidth, source.PixelHeight));

        // Extreme aspect ratios (e.g. a 160x60000 stitched scrolling capture) can push the minor
        // dimension below one pixel; clamp per axis so TransformedBitmap never gets a 0px size.
        double scaleX = Math.Max(scale, 1.0 / source.PixelWidth);
        double scaleY = Math.Max(scale, 1.0 / source.PixelHeight);
        var thumbnail = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
        thumbnail.Freeze();
        return thumbnail;
    }
}
