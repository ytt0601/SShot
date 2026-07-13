using System.ComponentModel;
using System.Windows;
using SShot.App.Resources;

namespace SShot.App.Views;

public partial class ScrollCaptureHudWindow : Window
{
    public event EventHandler? StopRequested;

    public ScrollCaptureHudWindow()
    {
        InitializeComponent();
    }

    public void UpdateFrameCount(int count)
    {
        StatusText.Text = string.Format(Strings.ScrollCaptureStatusFormat, count);
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    // WindowStyle="ToolWindow" gives this a native title-bar close button, which would otherwise
    // bypass StopRequested entirely and leave ScrollingCaptureOrchestrator's timer running forever.
    // ScrollingCaptureOrchestrator.Finish() already guards against being invoked twice, so it's
    // safe for this to also fire when Close() is called as part of the normal Stop-button path.
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
