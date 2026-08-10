using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.History;

namespace SShot.Core.Tests.History;

public class CaptureHistoryServiceTests
{
    private static BitmapSource NewImage(int width = 200, int height = 100) =>
        new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

    private static string NewTempFolderPath() =>
        Path.Combine(Path.GetTempPath(), "SShotTests_" + Guid.NewGuid());

    [Fact]
    public void Add_InsertsNewestFirst()
    {
        var service = new CaptureHistoryService(NewTempFolderPath(), capacity: 10);
        var first = service.Add(NewImage());
        var second = service.Add(NewImage());

        Assert.Equal(second.Id, service.Items[0].Id);
        Assert.Equal(first.Id, service.Items[1].Id);
    }

    [Fact]
    public void Add_BeyondCapacity_EvictsOldestAndDeletesItsFile()
    {
        string folder = NewTempFolderPath();
        var service = new CaptureHistoryService(folder, capacity: 3);
        var first = service.Add(NewImage());
        service.Add(NewImage());
        service.Add(NewImage());
        var fourth = service.Add(NewImage());

        Assert.Equal(3, service.Items.Count);
        Assert.DoesNotContain(service.Items, i => i.Id == first.Id);
        Assert.Equal(fourth.Id, service.Items[0].Id);
        Assert.False(File.Exists(first.FilePath));

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void Add_CreatesDownscaledThumbnail_PreservingAspectRatio()
    {
        var service = new CaptureHistoryService(NewTempFolderPath());
        var item = service.Add(NewImage(1600, 800));

        Assert.True(item.Thumbnail.PixelWidth <= 160);
        Assert.True(item.Thumbnail.PixelHeight <= 160);
        Assert.Equal(2.0, (double)item.Thumbnail.PixelWidth / item.Thumbnail.PixelHeight, 1);
    }

    [Fact]
    public void Add_ExtremeAspectRatio_ThumbnailKeepsAtLeastOnePixelPerAxis()
    {
        var service = new CaptureHistoryService(NewTempFolderPath());

        // A tall stitched scrolling capture: the uniform scale would shrink the minor
        // dimension below one pixel.
        var item = service.Add(NewImage(160, 60000));

        Assert.True(item.Thumbnail.PixelWidth >= 1);
        Assert.True(item.Thumbnail.PixelHeight >= 1);
    }

    [Fact]
    public void Add_SmallerThanThumbnailCap_IsNotUpscaled()
    {
        var service = new CaptureHistoryService(NewTempFolderPath());
        var item = service.Add(NewImage(50, 40));

        Assert.Equal(50, item.Thumbnail.PixelWidth);
        Assert.Equal(40, item.Thumbnail.PixelHeight);
    }

    [Fact]
    public async Task Add_PersistsToDisk_SoANewServiceInstanceReloadsIt()
    {
        string folder = NewTempFolderPath();
        var first = new CaptureHistoryService(folder, capacity: 10);
        first.Add(NewImage(300, 200));
        first.Add(NewImage(300, 200));

        var second = new CaptureHistoryService(folder, capacity: 10);
        await second.LoadFromDiskAsync();

        Assert.Equal(2, second.Items.Count);
        Assert.Equal(300, second.Items[0].Image.PixelWidth);
        Assert.Equal(200, second.Items[0].Image.PixelHeight);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Load_ReloadsNewestFirst_MatchingOriginalOrder()
    {
        string folder = NewTempFolderPath();
        var first = new CaptureHistoryService(folder, capacity: 10);
        var oldest = first.Add(NewImage());
        Thread.Sleep(10);
        var newest = first.Add(NewImage());

        var second = new CaptureHistoryService(folder, capacity: 10);
        await second.LoadFromDiskAsync();

        Assert.Equal(newest.CapturedAt, second.Items[0].CapturedAt, TimeSpan.FromMilliseconds(1));
        Assert.Equal(oldest.CapturedAt, second.Items[1].CapturedAt, TimeSpan.FromMilliseconds(1));

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Load_WhenMoreFilesOnDiskThanCapacity_PrunesOldestFiles()
    {
        string folder = NewTempFolderPath();
        var first = new CaptureHistoryService(folder, capacity: 10);
        first.Add(NewImage());
        Thread.Sleep(10);
        first.Add(NewImage());
        Thread.Sleep(10);
        first.Add(NewImage());

        var second = new CaptureHistoryService(folder, capacity: 2);
        await second.LoadFromDiskAsync();

        Assert.Equal(2, second.Items.Count);
        Assert.Equal(2, Directory.GetFiles(folder, "*.png").Length);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Load_WhenFolderDoesNotExist_StartsEmpty()
    {
        var service = new CaptureHistoryService(NewTempFolderPath(), capacity: 10);
        await service.LoadFromDiskAsync();

        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task Load_SkipsCorruptCacheFileWithoutThrowing()
    {
        string folder = NewTempFolderPath();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "20260101_000000_000_deadbeefdeadbeefdeadbeefdeadbeef.png"), "not a real png");

        var service = new CaptureHistoryService(folder, capacity: 10);
        await service.LoadFromDiskAsync();

        Assert.Empty(service.Items);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Load_AfterSessionCapturesExist_AppendsOlderItemsBehindThem()
    {
        string folder = NewTempFolderPath();
        var first = new CaptureHistoryService(folder, capacity: 10);
        first.Add(NewImage());
        Thread.Sleep(10);

        // Simulates a capture taken before the deferred startup load finishes: its own cache
        // file must not be re-loaded as a duplicate gallery entry.
        var second = new CaptureHistoryService(folder, capacity: 10);
        var sessionCapture = second.Add(NewImage());
        await second.LoadFromDiskAsync();

        Assert.Equal(2, second.Items.Count);
        Assert.Equal(sessionCapture.Id, second.Items[0].Id);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Load_WhenInMemoryItemsAlreadyAtCapacity_DeletesUnaddedOlderFileWithoutShowingIt()
    {
        string folder = NewTempFolderPath();
        var first = new CaptureHistoryService(folder, capacity: 10);
        var previousSession = first.Add(NewImage());

        // Simulates an in-memory-only session item (e.g. a prior SaveToDisk failure) filling the
        // single capacity slot before the deferred startup load resolves. Only one file exists on
        // disk, so LoadFromDisk's own on-disk pruning never has a count-based reason to remove it,
        // but there is no room left for it in the gallery - it must be deleted directly instead of
        // being added and then evicted, which would have briefly (or, in a synchronous merge,
        // never) surfaced a capture the user was never shown.
        var second = new CaptureHistoryService(folder, capacity: 1);
        second.Items.Add(new CaptureHistoryItem(Guid.NewGuid(), NewImage(), NewImage(), DateTime.Now, FilePath: null));

        await second.LoadFromDiskAsync();

        Assert.Single(second.Items);
        Assert.Null(second.Items[0].FilePath);
        Assert.False(File.Exists(previousSession.FilePath));

        Directory.Delete(folder, recursive: true);
    }
}
