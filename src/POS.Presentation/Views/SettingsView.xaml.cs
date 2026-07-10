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

        try
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
            _hasLoadedSettings = true;
        }
        catch (Exception ex)
        {
            viewModel.ShowError($"Error al cargar configuración: {ex.Message}");
            _hasLoadedSettings = false;
        }
    }
    private void BrowseBackupFolder_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Selecciona la carpeta de respaldo" };
        if (!string.IsNullOrWhiteSpace(viewModel.BackupDirectory))
        {
            dialog.InitialDirectory = viewModel.BackupDirectory;
        }

        if (dialog.ShowDialog() == true)
        {
            viewModel.BackupDirectory = dialog.FolderName;
        }
    }

    private void SmtpPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.SmtpPassword = passwordBox.Password;
        }
    }
}
