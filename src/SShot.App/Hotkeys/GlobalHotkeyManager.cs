using System.Windows;
using CommunityToolkit.Mvvm.Input;
using NHotkey;
using NHotkey.Wpf;
using SShot.App.Services;
using SShot.App.ViewModels;
using SShot.Core.Capture;
using SShot.Core.Settings;

namespace SShot.App.Hotkeys;

/// <summary>
/// Thin wrapper over NHotkey.Wpf, chosen over raw RegisterHotKey/HwndSource plumbing for lower
/// implementation/maintenance cost (see CLAUDE.md). Hotkey strings are parsed via
/// SShot.Core.Settings.HotkeyString so the same "Ctrl+Shift+R" format is used in settings.json.
/// </summary>
public sealed class GlobalHotkeyManager(CaptureGate captureGate)
{
    public bool Register(string name, string hotkeyString, EventHandler<HotkeyEventArgs> handler)
    {
        if (!HotkeyString.TryParse(hotkeyString, out var modifiers, out var key))
        {
            return false;
        }

        try
        {
            HotkeyManager.Current.AddOrReplace(name, key, modifiers, handler);
            return true;
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            return false;
        }
    }

    public void Unregister(string name)
    {
        try
        {
            HotkeyManager.Current.Remove(name);
        }
        catch (KeyNotFoundException)
        {
            // Not registered - nothing to do.
        }
    }

    /// <summary>
    /// (Re-)registers all four capture hotkeys from a settings snapshot - AddOrReplace makes
    /// this safe to call again with new key combos, so this is called both once at startup and
    /// again whenever Settings is saved, letting hotkey changes take effect immediately instead
    /// of requiring an app restart.
    /// </summary>
    public void RegisterCaptureHotkeys(MainViewModel viewModel, Window primaryWindow, AppSettings settings)
    {
        Register("SShot.CaptureFullScreen", settings.FullScreenHotkey, async (_, _) => await RunGatedCaptureAsync(primaryWindow, viewModel.CaptureFullScreenCommand));
        Register("SShot.CaptureRegion", settings.RegionHotkey, async (_, _) => await RunGatedCaptureAsync(primaryWindow, viewModel.CaptureRegionCommand));
        Register("SShot.CaptureWindow", settings.WindowHotkey, async (_, _) => await RunGatedCaptureAsync(primaryWindow, viewModel.CaptureWindowCommand));
        Register("SShot.CaptureScrolling", settings.ScrollingHotkey, async (_, _) => await RunGatedCaptureAsync(primaryWindow, viewModel.CaptureScrollingCommand));
    }

    /// <summary>
    /// Global hotkeys fire whether the primary window (MainWindow's Sidebar layout or
    /// FloatingToolbarWindow's pill) is currently on screen or hidden to tray. Only hide/restore it
    /// around the capture when it was actually visible beforehand - otherwise a tray-hidden window
    /// would flash on screen after every hotkey capture. This is the shared root cause fix: neither
    /// window's own Hide/Show wrapper (see their RunHiddenAsync) is reachable from this hotkey path,
    /// only from their own button clicks. Public so TrayIconManager can route tray-menu captures
    /// through the same gating/hide-show behavior instead of invoking the capture commands directly.
    /// </summary>
    public async Task RunGatedCaptureAsync(Window window, IAsyncRelayCommand command)
    {
        using var captureScope = captureGate.TryBeginScope();
        if (captureScope is null)
        {
            return;
        }

        bool wasVisible = window.IsVisible;
        if (wasVisible)
        {
            window.Hide();
            DwmSync.WaitForNextFrame();
        }

        try
        {
            await command.ExecuteAsync(null);
        }
        finally
        {
            if (wasVisible)
            {
                window.Show();
            }
        }
    }
}
