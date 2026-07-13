namespace SShot.App;

/// <summary>
/// Contract shared by the two interchangeable "primary window" implementations
/// (<see cref="MainWindow"/> for the Sidebar layout, <see cref="Views.FloatingToolbarWindow"/> for
/// the Floating layout - see AppSettings.UiLayoutMode). App.xaml.cs constructs whichever one the
/// user picked and wires the tray icon/hotkeys against this interface, so neither needs to know
/// which concrete layout is active.
/// </summary>
public interface IPrimaryAppWindow
{
    /// <summary>Called by the tray "ウィンドウを表示" menu item / double-click.</summary>
    void RestoreAndActivate();

    /// <summary>Called only by the tray "終了" menu item - lets Close() actually terminate the
    /// app instead of hiding to tray.</summary>
    void ExitApplication();
}
