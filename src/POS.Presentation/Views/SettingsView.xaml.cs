using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SmtpPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.SmtpPassword = passwordBox.Password;
        }
    }
}
