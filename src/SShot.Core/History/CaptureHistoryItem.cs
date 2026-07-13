using System.Windows.Media.Imaging;

namespace SShot.Core.History;

/// <summary>
/// <paramref name="FilePath"/> is the on-disk cache file backing this entry (see
/// CaptureHistoryService), used to delete it on eviction. It is an internal cache path, not the
/// user's chosen save location - unrelated to ImageFileService/SaveFolder.
/// </summary>
public sealed record CaptureHistoryItem(Guid Id, BitmapSource Image, BitmapSource Thumbnail, DateTime CapturedAt, string FilePath);
