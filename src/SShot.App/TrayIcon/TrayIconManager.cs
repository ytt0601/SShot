using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using SShot.App.Resources;
using SShot.App.ViewModels;

namespace SShot.App.TrayIcon;

/// <summary>
/// Wraps Hardcodet.NotifyIcon.Wpf's TaskbarIcon, chosen over WinForms NotifyIcon interop so the
/// context menu can be genuine WPF/MVVM-bindable rather than mixing a WinForms message loop into
/// a pure WPF app (see CLAUDE.md).
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;

    /// <summary>
    /// <paramref name="runGatedCapture"/> routes tray-triggered captures through the same
    /// CaptureGate + primary-window hide/show behavior as hotkeys (see
    /// GlobalHotkeyManager.RunGatedCaptureAsync) - binding MenuItem.Command straight to a
    /// MainViewModel capture command would invoke it directly, bypassing both the gate (letting a
    /// tray capture race a hotkey/button capture) and the hide/show wrapper (letting the primary
    /// window bake itself into the screenshot if visible).
    /// </summary>
    public TrayIconManager(
        MainViewModel mainViewModel,
        Func<IAsyncRelayCommand, Task> runGatedCapture,
        Action showMainWindow,
        Action exitApplication,
        Action openSettings)
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "SShot",
            Icon = LoadAppIcon(),
        };

        var menu = new ContextMenu();
        menu.Items.Add(CreateCaptureMenuItem(Strings.CaptureFullScreenButton, mainViewModel.CaptureFullScreenCommand, runGatedCapture));
        menu.Items.Add(CreateCaptureMenuItem(Strings.CaptureRegionButton, mainViewModel.CaptureRegionCommand, runGatedCapture));
        menu.Items.Add(CreateCaptureMenuItem(Strings.CaptureWindowButton, mainViewModel.CaptureWindowCommand, runGatedCapture));
        menu.Items.Add(CreateCaptureMenuItem(Strings.CaptureScrollingButton, mainViewModel.CaptureScrollingCommand, runGatedCapture));
        menu.Items.Add(new Separator());

        var showItem = new MenuItem { Header = Strings.ShowWindowMenuItem };
        showItem.Click += (_, _) => showMainWindow();
        menu.Items.Add(showItem);

        var settingsItem = new MenuItem { Header = Strings.OpenSettingsButton };
        settingsItem.Click += (_, _) => openSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = Strings.ExitMenuItem };
        exitItem.Click += (_, _) => exitApplication();
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = menu;
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => showMainWindow();
    }

    private static MenuItem CreateCaptureMenuItem(string header, IAsyncRelayCommand command, Func<IAsyncRelayCommand, Task> runGatedCapture)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await runGatedCapture(command);
        return item;
    }

    /// <summary>
    /// Reuses the icon already embedded in the exe via &lt;ApplicationIcon&gt; (Resources/Icons/app.ico)
    /// rather than shipping a second copy - works the same in dev (dotnet run) and single-file publish
    /// since the icon travels embedded in the exe either way.
    /// </summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        string? exePath = Environment.ProcessPath;
        var extracted = exePath is not null ? System.Drawing.Icon.ExtractAssociatedIcon(exePath) : null;
        return extracted ?? System.Drawing.SystemIcons.Application;
    }

    public void Dispose() => _taskbarIcon.Dispose();
}
