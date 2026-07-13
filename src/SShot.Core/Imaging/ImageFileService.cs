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
        return $"SShot_{timestamp:yyyyMMdd_HHmmss}.{extension}";
    }

    public string Save(BitmapSource image, string folderPath, ImageFileFormat format, DateTime? timestamp = null)
    {
        Directory.CreateDirectory(folderPath);
        string fileName = BuildFileName(timestamp ?? DateTime.Now, format);
        return SaveAs(image, Path.Combine(folderPath, fileName));
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
