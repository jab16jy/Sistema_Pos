using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class BackupView
{
    private bool _hasLoaded;

    public BackupView()
    {
        InitializeComponent();
    }

    private async void BackupView_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_hasLoaded || DataContext is not BackupViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
            _hasLoaded = true;
        }
        catch (Exception ex)
        {
            viewModel.ShowError($"Error al cargar respaldos: {ex.Message}");
            _hasLoaded = false;
        }
    }

    private void BrowseBackupFolder_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not BackupViewModel viewModel)
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
        if (DataContext is not BackupViewModel viewModel)
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
}
