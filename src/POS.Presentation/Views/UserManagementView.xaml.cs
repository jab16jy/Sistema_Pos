using System.Windows.Controls;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is UserManagementViewModel vm)
            {
                vm.ConfirmDeletionAsync = (title, message) =>
                {
                    var result = System.Windows.MessageBox.Show(
                        message,
                        title,
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                    return System.Threading.Tasks.Task.FromResult(result == System.Windows.MessageBoxResult.Yes);
                };
                vm.LoadUsersCommand.Execute(null);
            }
        };
    }

    // PasswordBox doesn't support binding, so push values to VM via code-behind
    private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.NewPassword = ((PasswordBox)sender).Password;
    }

    private void EditPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.EditPassword = ((PasswordBox)sender).Password;
    }
}
