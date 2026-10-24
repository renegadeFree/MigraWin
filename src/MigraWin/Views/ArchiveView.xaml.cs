using System.Windows;
using System.Windows.Controls;
using MigraWin.ViewModels;

namespace MigraWin.Views;

public partial class ArchiveView : UserControl
{
    public ArchiveView() => InitializeComponent();

    private void ArchivePasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetArchivePassword(ArchivePasswordBox.Password);
        }
    }

    private void NewRepositoryPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetNewRepositoryPassword(NewRepositoryPasswordBox.Password);
        }
    }

    private void NewRepositoryPasswordConfirmationBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetNewRepositoryPasswordConfirmation(NewRepositoryPasswordConfirmationBox.Password);
        }
    }
}
