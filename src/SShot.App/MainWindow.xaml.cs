using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using SShot.App.ViewModels;
using SShot.Core.Capture;

namespace SShot.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IPrimaryAppWindow
{
    private readonly MainViewModel _viewModel;
    private bool _isExiting;

    public MainWindow(MainViewModel viewModel, HistoryViewModel historyViewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        HistoryGallery.DataContext = historyViewModel;
        HistoryGallery.ItemActivated += (_, item) => viewModel.OpenHistoryItem(item);
    }

    private async void OnCaptureFullScreenClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureFullScreenCommand);

    private async void OnCaptureRegionClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureRegionCommand);

    private async void OnCaptureWindowClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureWindowCommand);

    private async void OnCaptureScrollingClick(object sender, RoutedEventArgs e) => await RunHiddenAsync(_viewModel.CaptureScrollingCommand);

    /// <summary>
    /// This window sits on screen like any other, so triggering a capture via its own buttons
    /// would bake it into the screenshot (the capture services BitBlt the screen directly - see
    /// FullScreenCaptureService/RegionCaptureService, neither hides the caller's window). Mirrors
    /// FloatingToolbarWindow.RunHiddenAsync; view-specific window management, kept out of
    /// MainViewModel per CLAUDE.md.
    /// </summary>
    private async Task RunHiddenAsync(IAsyncRelayCommand command)
    {
        Hide();
        DwmSync.WaitForNextFrame();
        try
        {
            await command.ExecuteAsync(null);
        }
        finally
        {
            Show();
        }
    }

    /// <summary>Called only by the tray "終了" menu item - lets this Close() actually terminate
    /// the app instead of hiding to tray.</summary>
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

    public void RestoreAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }
}
