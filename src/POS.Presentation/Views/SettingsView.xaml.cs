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

    private async void RestoreBackup_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecciona el archivo de respaldo a restaurar",
            Filter = "Respaldo POS (*.db)|*.db|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            "Esto reemplazará TODOS los datos actuales con el respaldo seleccionado y no se puede deshacer.\n\n" +
            "Se guardará una copia de los datos actuales (pos-prerestore-*.db) antes de continuar.\n\n¿Deseas continuar?",
            "Confirmar restauración",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        bool restored = await viewModel.RestoreAsync(dialog.FileName).ConfigureAwait(true);
        if (!restored)
        {
            return;
        }

        System.Windows.MessageBox.Show(
            "Restauración completada. La aplicación se reiniciará ahora.",
            "Restauración exitosa",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        string? executablePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(executablePath))
        {
            System.Diagnostics.Process.Start(executablePath);
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void SmtpPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.SmtpPassword = passwordBox.Password;
        }
    }
}
