using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SShot.App.Services;
using SShot.App.ViewModels;
using SShot.Core.Capture;

namespace SShot.App.Views;

/// <summary>
/// The "Floating" UiLayoutMode: a small always-on-top, borderless pill toolbar instead of a full
/// window (see MainWindow for the "Sidebar" alternative). Both share the same MainViewModel/
/// HistoryViewModel, so capture/history/settings behavior is identical - only presentation and
/// window chrome differ.
/// </summary>
public partial class FloatingToolbarWindow : Window, IPrimaryAppWindow
{
    private readonly MainViewModel _viewModel;
    private readonly CaptureGate _captureGate;
    private bool _isExiting;

    public FloatingToolbarWindow(MainViewModel viewModel, HistoryViewModel historyViewModel, CaptureGate captureGate)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _captureGate = captureGate;
        HistoryGallery.DataContext = historyViewModel;
        HistoryGallery.ItemActivated += (_, item) =>
        {
            HistoryPopup.IsOpen = false;
            viewModel.OpenHistoryItem(item);
        };

        Loaded += (_, _) => PositionNearTopCenter();
    }

    private void PositionNearTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - ActualWidth) / 2);
        Top = workArea.Top + 24;
    }

    private void OnPillMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            DragMove();
        }
    }

    private void OnHistoryToggleClick(object sender, RoutedEventArgs e) => HistoryPopup.IsOpen = !HistoryPopup.IsOpen;

    private void OnHideClick(object sender, RoutedEventArgs e) => Hide();

    private async void OnCaptureFullScreenClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureFullScreenCommand);

    private async void OnCaptureRegionClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureRegionCommand);

    private async void OnCaptureWindowClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureWindowCommand);

    private async void OnCaptureScrollingClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureScrollingCommand);

    /// <summary>
    /// This pill is Topmost and stays on screen, so triggering a capture while it's visible would
    /// bake it into the screenshot (the capture services BitBlt the screen directly - see
    /// FullScreenCaptureService/RegionCaptureService, neither hides the caller's window). Hiding
    /// around the capture keeps the floating layout correct; this is view-specific window
    /// management, so it stays in this code-behind rather than MainViewModel.
    /// </summary>
    private async Task RunHiddenAsync(IAsyncRelayCommand command)
    {
        if (!_captureGate.TryBegin())
        {
            return;
        }

        HistoryPopup.IsOpen = false;
        Hide();
        DwmSync.WaitForNextFrame();
        try
        {
            await command.ExecuteAsync(null);
        }
        finally
        {
            Show();
            _captureGate.End();
        }
    }

    public void RestoreAndActivate()
    {
        Show();
        Activate();
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
