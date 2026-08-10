using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace SShot.Core.Imaging;

public enum ImageFileFormat
{
    Png,
    Jpeg,
}

public sealed class ImageFileService
{
    public string BuildFileName(DateTime timestamp, ImageFileFormat format)
    {
        string extension = format == ImageFileFormat.Png ? "png" : "jpg";

        // Invariant culture: a culture-sensitive format would honor the OS calendar (e.g. Thai
        // Buddhist year 2569) and diverge from CaptureHistoryService's invariant filenames.
        return $"SShot_{timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.{extension}";
    }

    public string Save(BitmapSource image, string folderPath, ImageFileFormat format, DateTime? timestamp = null)
    {
        Directory.CreateDirectory(folderPath);
        string fileName = BuildFileName(timestamp ?? DateTime.Now, format);
        string fullPath = MakeUniquePath(Path.Combine(folderPath, fileName));
        return SaveAs(image, fullPath);
    }

    /// <summary>BuildFileName has only second precision, so two captures saved within the same
    /// second would otherwise collide - unlike SaveAs (an explicit path the user picked, e.g. via
    /// a save dialog, where overwriting is the expected/intended action), an auto-generated name
    /// colliding should never silently destroy a prior screenshot.</summary>
    private static string MakeUniquePath(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        string extension = Path.GetExtension(fullPath);

        for (int counter = 2; ; counter++)
        {
            string candidate = Path.Combine(directory, $"{nameWithoutExtension}_{counter}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Saves directly to an explicit path (e.g. one chosen via a save-file dialog).
    /// Format is inferred from the file extension; unrecognized extensions default to PNG.</summary>
    public string SaveAs(BitmapSource image, string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string extension = Path.GetExtension(fullPath);
        bool isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        BitmapEncoder encoder = isJpeg ? new JpegBitmapEncoder() : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);

        return fullPath;
    }
}
