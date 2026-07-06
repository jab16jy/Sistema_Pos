using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class SettingsView
{
    private bool _hasLoadedSettings;

    public SettingsView()
    {
        InitializeComponent();
    }

    private async void SettingsView_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_hasLoadedSettings || DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        _hasLoadedSettings = true;
        await viewModel.LoadAsync().ConfigureAwait(true);
    }
    private void SmtpPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.SmtpPassword = passwordBox.Password;
        }
    }
}
