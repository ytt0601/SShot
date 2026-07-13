using System.IO;
using SShot.Core.Imaging;

namespace SShot.Core.Tests.Imaging;

public class ImageFileServiceTests
{
    [Fact]
    public void BuildFileName_Png_UsesTimestampAndPngExtension()
    {
        var service = new ImageFileService();
        var timestamp = new DateTime(2026, 7, 8, 22, 37, 43);

        string fileName = service.BuildFileName(timestamp, ImageFileFormat.Png);

        Assert.Equal("SShot_20260708_223743.png", fileName);
    }

    [Fact]
    public void BuildFileName_Jpeg_UsesJpgExtension()
    {
        var service = new ImageFileService();
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5);

        string fileName = service.BuildFileName(timestamp, ImageFileFormat.Jpeg);

        Assert.Equal("SShot_20260102_030405.jpg", fileName);
    }

    [Fact]
    public void Save_WritesFileToFolder_AndReturnsFullPath()
    {
        var service = new ImageFileService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "SShotTests_" + Guid.NewGuid());
        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);

        try
        {
            string path = service.Save(bitmap, tempFolder, ImageFileFormat.Png, new DateTime(2026, 1, 1));

            Assert.True(File.Exists(path));
            Assert.StartsWith(tempFolder, path);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void Save_CalledTwiceWithSameTimestamp_DoesNotOverwritePriorFile()
    {
        var service = new ImageFileService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "SShotTests_" + Guid.NewGuid());
        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0);

        try
        {
            string firstPath = service.Save(bitmap, tempFolder, ImageFileFormat.Png, timestamp);
            string secondPath = service.Save(bitmap, tempFolder, ImageFileFormat.Png, timestamp);

            Assert.NotEqual(firstPath, secondPath);
            Assert.True(File.Exists(firstPath));
            Assert.True(File.Exists(secondPath));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
