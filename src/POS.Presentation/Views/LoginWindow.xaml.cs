using System.Windows;
using POS.Presentation.ViewModels;

namespace POS.Presentation.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginSucceeded += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
