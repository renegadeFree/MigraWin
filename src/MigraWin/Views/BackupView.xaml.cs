using System.Windows;
using System.Windows.Controls;
using MigraWin.ViewModels;

namespace MigraWin.Views;

public partial class BackupView : UserControl
{
    public BackupView() => InitializeComponent();

    private void BackupPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetBackupPassword(BackupPasswordBox.Password);
        }
    }

    private void BackupPasswordConfirmationBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetBackupPasswordConfirmation(BackupPasswordConfirmationBox.Password);
        }
    }
}
