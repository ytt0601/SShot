using System.IO;

namespace SShot.Core.Settings;

public sealed class AppSettings
{
    public string SaveFolder { get; set; } = DefaultSaveFolder();

    public bool SaveAsJpeg { get; set; }

    public bool AutoStartEnabled { get; set; }

    /// <summary>"ja" or "en". Restart-required to apply; live switching is out of scope for
    /// the MVP.</summary>
    public string UiLanguage { get; set; } = "ja";

    public string FullScreenHotkey { get; set; } = "Ctrl+Shift+F";

    public string RegionHotkey { get; set; } = "Ctrl+Shift+R";

    public string WindowHotkey { get; set; } = "Ctrl+Shift+W";

    public string ScrollingHotkey { get; set; } = "Ctrl+Shift+S";

    /// <summary>"Sidebar" or "Floating". Restart-required to apply - switching layouts means
    /// swapping which Window is constructed and wired to the tray/hotkeys in App.xaml.cs, so
    /// (like UiLanguage) live re-switching is out of scope for the MVP.</summary>
    public string UiLayoutMode { get; set; } = "Sidebar";

    /// <summary>One of the ids in ThemeCatalog. Applies immediately on Settings Save (no restart)
    /// since it's just a ResourceDictionary swap - see ThemeService.</summary>
    public string ColorTheme { get; set; } = "Azure";

    public static string DefaultSaveFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SShot");
}
