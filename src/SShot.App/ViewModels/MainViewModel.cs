using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SShot.App.Hotkeys;
using SShot.App.Resources;
using SShot.App.Services;
using SShot.App.Views;
using SShot.Core.Annotation;
using SShot.Core.Capture;
using SShot.Core.History;
using SShot.Core.Imaging;
using SShot.Core.Models;
using SShot.Core.Settings;

namespace SShot.App.ViewModels;

public partial class MainViewModel(
    FullScreenCaptureService fullScreenCapture,
    RegionCaptureService regionCapture,
    WindowPickerCaptureService windowPickerCapture,
    ScrollingCaptureOrchestrator scrollingCapture,
    ImageFileService imageFileService,
    CaptureHistoryService historyService,
    SettingsService settingsService,
    AutoStartService autoStartService,
    ThemeService themeService,
    GlobalHotkeyManager hotkeyManager) : ObservableObject
{
    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Which content pane the sidebar layout shows (Capture vs. History). Pure view
    /// state, not domain logic, but kept in the ViewModel under the "no logic in View
    /// code-behind" rule.</summary>
    [ObservableProperty]
    private bool _isHistoryPaneActive;

    public bool IsCapturePaneActive => !IsHistoryPaneActive;

    partial void OnIsHistoryPaneActiveChanged(bool value) => OnPropertyChanged(nameof(IsCapturePaneActive));

    [RelayCommand]
    private void ShowCapturePane() => IsHistoryPaneActive = false;

    [RelayCommand]
    private void ShowHistoryPane() => IsHistoryPaneActive = true;

    [RelayCommand]
    private async Task CaptureFullScreenAsync()
    {
        var result = await fullScreenCapture.CaptureAsync();
        ApplyResult(result, Strings.ModeNameFullScreen);
    }

    [RelayCommand]
    private async Task CaptureRegionAsync()
    {
        var result = await regionCapture.CaptureAsync();
        ApplyResult(result, Strings.ModeNameRegion);
    }

    [RelayCommand]
    private async Task CaptureWindowAsync()
    {
        var result = await windowPickerCapture.CaptureAsync();
        ApplyResult(result, Strings.ModeNameWindow);
    }

    [RelayCommand]
    private async Task CaptureScrollingAsync()
    {
        var result = await scrollingCapture.CaptureAsync();
        ApplyResult(result, Strings.ModeNameScrolling);
    }

    [RelayCommand(CanExecute = nameof(HasPreview))]
    private void CopyToClipboard()
    {
        if (PreviewImage is null)
        {
            return;
        }

        Clipboard.SetImage(PreviewImage);
        StatusMessage = Strings.ClipboardCopiedMessage;
    }

    [RelayCommand(CanExecute = nameof(HasPreview))]
    private void SaveToFile()
    {
        if (PreviewImage is null)
        {
            return;
        }

        var settings = settingsService.Load();
        var format = settings.SaveAsJpeg ? ImageFileFormat.Jpeg : ImageFileFormat.Png;
        string path = imageFileService.Save(PreviewImage, settings.SaveFolder, format);
        StatusMessage = string.Format(Strings.SavedMessageFormat, path);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = new SettingsViewModel(settingsService, autoStartService, themeService);
        // Hotkeys are otherwise only ever registered once, at app startup - without this, saving
        // a changed hotkey here would silently have no effect until the app restarts.
        settingsViewModel.SettingsSaved += OnSettingsSaved;
        var settingsWindow = new SettingsWindow(settingsViewModel) { Owner = Application.Current.MainWindow };
        settingsWindow.ShowDialog();
    }

    private void OnSettingsSaved(object? sender, AppSettings settings)
    {
        if (Application.Current.MainWindow is Window primaryWindow)
        {
            hotkeyManager.RegisterCaptureHotkeys(this, primaryWindow, settings);
        }
    }

    [RelayCommand(CanExecute = nameof(HasPreview))]
    private void OpenEditor()
    {
        if (PreviewImage is null)
        {
            return;
        }

        var document = new AnnotationDocument(PreviewImage);
        var editorViewModel = new EditorViewModel(document);
        var editorWindow = new EditorWindow(editorViewModel, imageFileService, settingsService) { Owner = Application.Current.MainWindow };
        editorWindow.ShowDialog();

        if (editorWindow.Result is not null)
        {
            PreviewImage = editorWindow.Result;
            StatusMessage = Strings.EditAppliedMessage;
        }
    }

    public void OpenHistoryItem(CaptureHistoryItem item)
    {
        PreviewImage = item.Image;
        StatusMessage = string.Format(Strings.HistoryOpenedMessageFormat, item.CapturedAt);
        CopyToClipboardCommand.NotifyCanExecuteChanged();
        SaveToFileCommand.NotifyCanExecuteChanged();
        OpenEditorCommand.NotifyCanExecuteChanged();
        OpenEditor();
    }

    private bool HasPreview() => PreviewImage is not null;

    private void ApplyResult(CaptureResult? result, string modeName)
    {
        if (result is null)
        {
            StatusMessage = string.Format(Strings.CaptureCancelledMessageFormat, modeName);
            return;
        }

        PreviewImage = result.Image;
        StatusMessage = string.Format(Strings.CaptureSucceededMessageFormat, modeName, result.Image.PixelWidth, result.Image.PixelHeight);
        historyService.Add(result.Image);
        CopyToClipboardCommand.NotifyCanExecuteChanged();
        SaveToFileCommand.NotifyCanExecuteChanged();
        OpenEditorCommand.NotifyCanExecuteChanged();
    }
}
