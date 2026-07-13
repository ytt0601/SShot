using System.Windows.Media.Imaging;

namespace SShot.Core.History;

/// <summary>
/// <paramref name="FilePath"/> is the on-disk cache file backing this entry (see
/// CaptureHistoryService), used to delete it on eviction. It is an internal cache path, not the
/// user's chosen save location - unrelated to ImageFileService/SaveFolder. Null when the cache
/// write itself failed (e.g. disk full, AV lock) - the item still exists for this session's
/// gallery, it just isn't backed by a file to persist across a restart or delete on eviction.
/// </summary>
public sealed record CaptureHistoryItem(Guid Id, BitmapSource Image, BitmapSource Thumbnail, DateTime CapturedAt, string? FilePath);
