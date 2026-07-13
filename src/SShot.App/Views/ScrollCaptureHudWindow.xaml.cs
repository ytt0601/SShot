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
}
