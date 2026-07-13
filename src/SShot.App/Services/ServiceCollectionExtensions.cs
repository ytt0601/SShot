using Microsoft.Extensions.DependencyInjection;
using SShot.App.Hotkeys;
using SShot.App.ViewModels;
using SShot.App.Views;
using SShot.Core.Capture;
using SShot.Core.History;
using SShot.Core.Imaging;
using SShot.Core.Settings;

namespace SShot.App.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<FullScreenCaptureService>();
        services.AddSingleton<RegionCaptureService>();
        services.AddSingleton<WindowCaptureService>();
        services.AddSingleton<WindowPickerCaptureService>();
        services.AddSingleton<ScrollingCaptureService>();
        services.AddSingleton<ScrollingCaptureOrchestrator>();
        services.AddSingleton<ImageFileService>();
        services.AddSingleton<CaptureHistoryService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AutoStartService>();
        services.AddSingleton<GlobalHotkeyManager>();
        services.AddSingleton<ThemeService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<FloatingToolbarWindow>();

        return services;
    }
}
