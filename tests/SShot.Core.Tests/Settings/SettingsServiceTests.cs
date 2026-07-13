using System.IO;
using SShot.Core.Settings;

namespace SShot.Core.Tests.Settings;

public class SettingsServiceTests
{
    private static string NewTempFilePath() =>
        Path.Combine(Path.GetTempPath(), "SShotTests_" + Guid.NewGuid(), "settings.json");

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var service = new SettingsService(NewTempFilePath());

        var settings = service.Load();

        Assert.Equal(AppSettings.DefaultSaveFolder(), settings.SaveFolder);
        Assert.False(settings.SaveAsJpeg);
        Assert.False(settings.AutoStartEnabled);
        Assert.Equal("Sidebar", settings.UiLayoutMode);
        Assert.Equal("Azure", settings.ColorTheme);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        string path = NewTempFilePath();
        var service = new SettingsService(path);
        var original = new AppSettings
        {
            SaveFolder = @"D:\Screenshots",
            SaveAsJpeg = true,
            AutoStartEnabled = true,
            FullScreenHotkey = "Ctrl+Alt+F",
            RegionHotkey = "Ctrl+Alt+R",
            WindowHotkey = "Ctrl+Alt+W",
            ScrollingHotkey = "Ctrl+Alt+S",
            UiLayoutMode = "Floating",
            ColorTheme = "Midnight",
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.Equal(original.SaveFolder, loaded.SaveFolder);
        Assert.Equal(original.SaveAsJpeg, loaded.SaveAsJpeg);
        Assert.Equal(original.AutoStartEnabled, loaded.AutoStartEnabled);
        Assert.Equal(original.FullScreenHotkey, loaded.FullScreenHotkey);
        Assert.Equal(original.RegionHotkey, loaded.RegionHotkey);
        Assert.Equal(original.WindowHotkey, loaded.WindowHotkey);
        Assert.Equal(original.ScrollingHotkey, loaded.ScrollingHotkey);
        Assert.Equal(original.UiLayoutMode, loaded.UiLayoutMode);
        Assert.Equal(original.ColorTheme, loaded.ColorTheme);

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaultsInsteadOfThrowing()
    {
        string path = NewTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not valid json");
        var service = new SettingsService(path);

        var settings = service.Load();

        Assert.Equal(AppSettings.DefaultSaveFolder(), settings.SaveFolder);

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void Load_WhenFileIsLocked_ReturnsDefaultsInsteadOfThrowing()
    {
        string path = NewTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        var service = new SettingsService(path);

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var settings = service.Load();

            Assert.Equal(AppSettings.DefaultSaveFolder(), settings.SaveFolder);
        }

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
