using System.Windows.Controls;

namespace POS.Presentation.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        LoginPasswordBox.PasswordChanged += OnPasswordChanged;
    }

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LoginViewModel vm)
        {
            vm.Password = LoginPasswordBox.Password;
        }
    }
}
