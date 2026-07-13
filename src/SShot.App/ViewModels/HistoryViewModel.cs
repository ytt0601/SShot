using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SShot.Core.History;

namespace SShot.App.ViewModels;

public partial class HistoryViewModel(CaptureHistoryService historyService) : ObservableObject
{
    public ObservableCollection<CaptureHistoryItem> Items => historyService.Items;
}
