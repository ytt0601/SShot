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

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
