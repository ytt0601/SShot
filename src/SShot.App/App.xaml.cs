using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SShot.App.Hotkeys;
using SShot.App.Resources;
using SShot.App.Services;
using SShot.App.TrayIcon;
using SShot.App.ViewModels;
using SShot.App.Views;
using SShot.Core.History;
using SShot.Core.Settings;

namespace SShot.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Fixed name tied to this app's installer UpgradeCode (installer/wix/Product.wxs) so it can't
    // collide with an unrelated app's single-instance mutex. Session-scoped (no "Global\" prefix)
    // since SShot is a per-user desktop app, not a service shared across sessions.
    private const string SingleInstanceMutexName = "SShot-SingleInstance-5F3DDE94-D3D7-48E6-B51F-9DA409897D54";

    public static IServiceProvider Services { get; private set; } = null!;

    private TrayIconManager? _trayIconManager;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global hotkey handlers are async void delegates (see GlobalHotkeyManager) - any
        // exception thrown while awaiting a capture would otherwise propagate unhandled and
        // silently kill this tray-resident process. This is the app's boundary of last resort;
        // it doesn't attempt to keep the triggering operation's window state consistent, just
        // keeps the process (and the tray icon) alive so the user isn't left with a vanished app.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out bool isNewInstance);
        _ownsSingleInstanceMutex = isNewInstance;
        if (!isNewInstance)
        {
            // No DI container yet at this point - building the full container just to show one
            // message box before exiting would be wasted work, so read settings directly.
            var earlySettings = new SettingsService().Load();
            ApplyUiCulture(earlySettings.UiLanguage);
            MessageBox.Show(Strings.AlreadyRunningMessage, "SShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddAppServices();
        Services = serviceCollection.BuildServiceProvider();

        var settingsService = Services.GetRequiredService<SettingsService>();
        var settings = settingsService.Load();

        // Must happen before any Window/UserControl is constructed - {x:Static Strings.X}
        // resource lookups resolve against CurrentUICulture at construction time, and MVP-scope
        // localization is restart-based (no live re-switching), see CLAUDE.md.
        ApplyUiCulture(settings.UiLanguage);

        // Also settled before any window is constructed, for the same reason: DynamicResource
        // lookups would otherwise briefly show the App.xaml default (Azure) theme.
        Services.GetRequiredService<ThemeService>().Apply(settings.ColorTheme);

        // UiLayoutMode picks which concrete IPrimaryAppWindow gets constructed and wired to the
        // tray/hotkeys below; switching it is restart-required (see AppSettings.UiLayoutMode).
        IPrimaryAppWindow primaryWindow = settings.UiLayoutMode == "Floating"
            ? Services.GetRequiredService<FloatingToolbarWindow>()
            : Services.GetRequiredService<MainWindow>();

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        var hotkeyManager = Services.GetRequiredService<GlobalHotkeyManager>();

        hotkeyManager.RegisterCaptureHotkeys(mainViewModel, (Window)primaryWindow, settings);

        _trayIconManager = new TrayIconManager(
            mainViewModel,
            runGatedCapture: command => hotkeyManager.RunGatedCaptureAsync((Window)primaryWindow, command),
            showMainWindow: primaryWindow.RestoreAndActivate,
            exitApplication: () =>
            {
                primaryWindow.ExitApplication();
                Shutdown();
            },
            openSettings: () => mainViewModel.OpenSettingsCommand.Execute(null));

        ((Window)primaryWindow).Show();

        // After Show(), so decoding the previous session's history PNGs never delays first
        // paint (see CaptureHistoryService.LoadFromDiskAsync).
        _ = Services.GetRequiredService<CaptureHistoryService>().LoadFromDiskAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static void ApplyUiCulture(string uiLanguage)
    {
        var culture = new CultureInfo(uiLanguage == "en" ? "en-US" : "ja-JP");
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            string.Format(Strings.UnexpectedErrorMessageFormat, e.Exception.Message),
            "SShot", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
