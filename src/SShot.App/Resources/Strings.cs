using System.Resources;

namespace SShot.App.Resources;

/// <summary>
/// Hand-written wrapper around the compiled Strings.resx/Strings.ja.resx satellite resources
/// (not a Visual Studio designer-generated class, since this project is built with the plain
/// dotnet CLI). XAML references these via {x:Static resources:Strings.SomeKey}, which resolves
/// at window-construction time - so the UI language is fixed by whatever CultureInfo.CurrentUICulture
/// is set to in App.OnStartup before any window is created (restart-based switching, see CLAUDE.md).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("SShot.App.Resources.Strings", typeof(Strings).Assembly);

    public static string CaptureFullScreenButton => Get();
    public static string CaptureRegionButton => Get();
    public static string CaptureWindowButton => Get();
    public static string CaptureScrollingButton => Get();
    public static string WindowPickerHintDefault => Get();
    public static string WindowPickerHintChildMode => Get();
    public static string OpenEditorButton => Get();
    public static string CopyToClipboardButton => Get();
    public static string SaveToFileButton => Get();
    public static string OpenSettingsButton => Get();

    public static string SettingsWindowTitle => Get();
    public static string SaveFolderLabel => Get();
    public static string BrowseButton => Get();
    public static string SaveAsJpegCheckbox => Get();
    public static string AutoStartCheckbox => Get();
    public static string UiLanguageLabel => Get();
    public static string HotkeysSectionLabel => Get();
    public static string HotkeyHintText => Get();
    public static string SaveButton => Get();
    public static string CloseButton => Get();

    public static string UiLayoutModeLabel => Get();
    public static string UiLayoutModeSidebar => Get();
    public static string UiLayoutModeFloating => Get();
    public static string UiLayoutModeHintText => Get();

    public static string ColorThemeLabel => Get();
    public static string ThemeAzure => Get();
    public static string ThemeEmerald => Get();
    public static string ThemeSunset => Get();
    public static string ThemeGrape => Get();
    public static string ThemeAmber => Get();
    public static string ThemeCrimson => Get();
    public static string ThemeOcean => Get();
    public static string ThemeRose => Get();
    public static string ThemeSlateDark => Get();
    public static string ThemeMidnight => Get();

    public static string NavCaptureSectionLabel => Get();
    public static string NavHistorySectionLabel => Get();
    public static string HideToolbarButton => Get();

    public static string ScrollCaptureStatusFormat => Get();
    public static string StopAndStitchButton => Get();

    public static string EditorWindowTitle => Get();
    public static string ToolSelect => Get();
    public static string ToolRectangle => Get();
    public static string ToolEllipse => Get();
    public static string ToolArrow => Get();
    public static string ToolFreehand => Get();
    public static string ToolHighlighter => Get();
    public static string ToolText => Get();
    public static string ToolStepStamp => Get();
    public static string ToolMosaic => Get();
    public static string ToolBlur => Get();
    public static string ToolCrop => Get();
    public static string CropByCoordinatesButton => Get();
    public static string CropCoordinatesWindowTitle => Get();
    public static string CropXLabel => Get();
    public static string CropYLabel => Get();
    public static string CropWidthLabel => Get();
    public static string CropHeightLabel => Get();
    public static string CropInvalidRangeMessage => Get();
    public static string OkButton => Get();
    public static string CancelButton => Get();
    public static string MosaicIntensityLabel => Get();
    public static string BlurIntensityLabel => Get();
    public static string ApplyMosaicWholeButton => Get();
    public static string ApplyBlurWholeButton => Get();
    public static string DeleteButton => Get();
    public static string BringToFrontButton => Get();
    public static string SendToBackButton => Get();
    public static string UndoButton => Get();
    public static string RedoButton => Get();
    public static string ToolStatusFormat => Get();
    public static string SavedMessageFormat => Get();

    public static string CategoryTools => Get();
    public static string CategoryMosaicBlur => Get();
    public static string CategoryColor => Get();
    public static string CategoryArrange => Get();
    public static string CategoryFile => Get();
    public static string SaveAsButton => Get();
    public static string OverwriteSaveButton => Get();
    public static string SaveAsDialogTitle => Get();
    public static string ImageFileDialogFilter => Get();
    public static string UndoTooltip => Get();
    public static string RedoTooltip => Get();

    public static string ShowWindowMenuItem => Get();
    public static string ExitMenuItem => Get();

    public static string ModeNameFullScreen => Get();
    public static string ModeNameRegion => Get();
    public static string ModeNameWindow => Get();
    public static string ModeNameScrolling => Get();
    public static string ClipboardCopiedMessage => Get();
    public static string EditAppliedMessage => Get();
    public static string HistoryOpenedMessageFormat => Get();
    public static string CaptureCancelledMessageFormat => Get();
    public static string CaptureSucceededMessageFormat => Get();

    public static string AlreadyRunningMessage => Get();
    public static string UnexpectedErrorMessageFormat => Get();

    private static string Get([System.Runtime.CompilerServices.CallerMemberName] string key = "") =>
        ResourceManager.GetString(key) ?? key;
}
