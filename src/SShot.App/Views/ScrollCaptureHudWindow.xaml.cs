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

    /// <summary>True from the moment this window starts closing - by its own title-bar close
    /// button, by Alt+F4, or by a Close() call. ScrollingCaptureOrchestrator tests this before
    /// calling Close() itself: re-entering Close() from inside the close that raised
    /// StopRequested lets the nested call finish the close, after which the outer one throws
    /// InvalidOperationException ("cannot call Close on a Window that has closed").</summary>
    public bool IsClosing { get; private set; }

    // WindowStyle="ToolWindow" gives this a native title-bar close button, which would otherwise
    // bypass StopRequested entirely and leave ScrollingCaptureOrchestrator's timer running forever.
    // ScrollingCaptureOrchestrator.Finish() already guards against being invoked twice, so it's
    // safe for this to also fire when Close() is called as part of the normal Stop-button path.
    protected override void OnClosing(CancelEventArgs e)
    {
        // Set before StopRequested is raised: the handler tears the session down synchronously
        // and has to already see this window as closing.
        IsClosing = true;
        base.OnClosing(e);
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
