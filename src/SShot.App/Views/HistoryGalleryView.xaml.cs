using System.Windows;
using System.Windows.Controls;
using SShot.Core.History;

namespace SShot.App.Views;

public partial class HistoryGalleryView : UserControl
{
    public event EventHandler<CaptureHistoryItem>? ItemActivated;

    public HistoryGalleryView()
    {
        InitializeComponent();
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CaptureHistoryItem item })
        {
            ItemActivated?.Invoke(this, item);
        }
    }
}
