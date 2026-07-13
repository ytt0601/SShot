using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SShot.App.Resources;
using SShot.App.Resources.Themes;
using SShot.App.Services;
using SShot.Core.Settings;

namespace SShot.App.ViewModels;

/// <summary>A single ComboBox entry: a persisted id paired with its localized display name.</summary>
public sealed record SettingOption(string Id, string DisplayName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly AutoStartService _autoStartService;
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private string _saveFolder;

    [ObservableProperty]
    private bool _saveAsJpeg;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private string _uiLanguage;

    [ObservableProperty]
    private string _uiLayoutMode;

    [ObservableProperty]
    private string _colorTheme;

    [ObservableProperty]
    private string _fullScreenHotkey;

    [ObservableProperty]
    private string _regionHotkey;

    [ObservableProperty]
    private string _windowHotkey;

    [ObservableProperty]
    private string _scrollingHotkey;

    public ObservableCollection<SettingOption> UiLayoutModeOptions { get; } =
    [
        new("Sidebar", Strings.UiLayoutModeSidebar),
        new("Floating", Strings.UiLayoutModeFloating),
    ];

    public ObservableCollection<SettingOption> ColorThemeOptions { get; } =
        new(ThemeCatalog.Ids.Select(id => new SettingOption(id, ThemeCatalog.DisplayName(id))));

    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsViewModel(SettingsService settingsService, AutoStartService autoStartService, ThemeService themeService)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _themeService = themeService;

        var settings = _settingsService.Load();
        _saveFolder = settings.SaveFolder;
        _saveAsJpeg = settings.SaveAsJpeg;
        _autoStartEnabled = _autoStartService.IsEnabled();
        _uiLanguage = settings.UiLanguage;
        _uiLayoutMode = settings.UiLayoutMode;
        _colorTheme = settings.ColorTheme;
        _fullScreenHotkey = settings.FullScreenHotkey;
        _regionHotkey = settings.RegionHotkey;
        _windowHotkey = settings.WindowHotkey;
        _scrollingHotkey = settings.ScrollingHotkey;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(SaveFolder) ? SaveFolder : AppSettings.DefaultSaveFolder(),
        };

        if (dialog.ShowDialog() == true)
        {
            SaveFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            SaveFolder = SaveFolder,
            SaveAsJpeg = SaveAsJpeg,
            AutoStartEnabled = AutoStartEnabled,
            UiLanguage = UiLanguage,
            UiLayoutMode = UiLayoutMode,
            ColorTheme = ColorTheme,
            FullScreenHotkey = FullScreenHotkey,
            RegionHotkey = RegionHotkey,
            WindowHotkey = WindowHotkey,
            ScrollingHotkey = ScrollingHotkey,
        };

        _settingsService.Save(settings);
        _autoStartService.SetEnabled(AutoStartEnabled, Environment.ProcessPath ?? string.Empty);
        // Unlike UiLayoutMode (restart-required), the color theme is just a ResourceDictionary
        // swap, so it can apply live the moment Settings is saved - see ThemeService.
        _themeService.Apply(ColorTheme);
        SettingsSaved?.Invoke(this, settings);
    }
}
