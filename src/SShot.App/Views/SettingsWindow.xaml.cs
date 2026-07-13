using System.Windows;
using SShot.App.ViewModels;

namespace SShot.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SaveCommand.Execute(null);
        }

        Close();
    }

    // Close button also has IsCancel="True" (so Escape works too); WPF's IsCancel handling sets
    // DialogResult itself after this handler returns, which implicitly closes the window - calling
    // Close() here directly would make that second, redundant close throw InvalidOperationException.
    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
