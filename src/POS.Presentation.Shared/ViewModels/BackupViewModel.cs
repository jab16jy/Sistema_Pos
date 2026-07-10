using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Settings;
using POS.Application.UseCases.Settings;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// Dedicated "Respaldos" module: manual database backup and restore, kept separate from the
/// Configuración form so each admin concern stays a self-contained module.
/// </summary>
public sealed class BackupViewModel : ViewModelBase
{
    private readonly LoadSettingsUseCase _loadSettings;
    private readonly TriggerManualBackupUseCase _triggerBackup;
    private readonly UpdateBackupDirectoryUseCase? _updateBackupDirectory;
    private readonly RestoreBackupUseCase? _restoreBackup;

    private string? _backupDirectory;
    private ManualBackupMetadata? _lastSuccessfulBackup;
    private string _message = "Elige la carpeta destino y crea un respaldo de la base de datos.";

    public BackupViewModel(
        LoadSettingsUseCase loadSettings,
        TriggerManualBackupUseCase triggerBackup,
        UpdateBackupDirectoryUseCase? updateBackupDirectory = null,
        RestoreBackupUseCase? restoreBackup = null)
    {
        _loadSettings = loadSettings;
        _triggerBackup = triggerBackup;
        _updateBackupDirectory = updateBackupDirectory;
        _restoreBackup = restoreBackup;

        BackupCommand = new RelayCommand(_ => RunSafe(BackupAsync(), ex => Message = $"Error inesperado al crear backup: {ex.Message}"));
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar respaldos: {ex.Message}"));
    }

    public ICommand BackupCommand { get; }

    public ICommand LoadCommand { get; }

    public string? BackupDirectory
    {
        get => _backupDirectory;
        set
        {
            if (SetProperty(ref _backupDirectory, value))
            {
                OnPropertyChanged(nameof(IsBackupConfigured));
            }
        }
    }

    public bool IsBackupConfigured => !string.IsNullOrWhiteSpace(BackupDirectory);

    /// <summary>True when a restore use case was injected (i.e. restore is available in this environment).</summary>
    public bool CanRestore => _restoreBackup is not null;

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public void ShowError(string message) => Message = message;

    public string LastBackupSummary => _lastSuccessfulBackup is null
        ? "Sin backup exitoso"
        : _lastSuccessfulBackup.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public string LastBackupTimestampUtc => _lastSuccessfulBackup is null
        ? "Sin datos"
        : _lastSuccessfulBackup.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    public string LastBackupSize => _lastSuccessfulBackup is null ? "Sin datos" : $"{_lastSuccessfulBackup.SizeBytes} bytes";

    public string LastBackupLocation => _lastSuccessfulBackup?.Location ?? "Sin datos";

    public string LastBackupOperator => _lastSuccessfulBackup?.OperatorUsername ?? "No disponible";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SettingsResult? settings = await _loadSettings.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            if (settings is null)
            {
                Message = "Aún no hay configuración guardada. Guarda los datos del negocio en Configuración antes de respaldar.";
                return;
            }

            BackupDirectory = settings.BackupDirectory;
            UpdateLastSuccessfulBackup(settings.LastSuccessfulBackup);

            Message = _lastSuccessfulBackup is null
                ? "Sin respaldos previos. Elige una carpeta y crea el primero."
                : "Respaldos cargados.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar respaldos: {ex.Message}";
        }
    }

    public async Task BackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Remember the chosen folder for next time (best-effort; no-op if settings aren't saved yet).
            if (_updateBackupDirectory is not null)
            {
                await _updateBackupDirectory.ExecuteAsync(BackupDirectory, cancellationToken).ConfigureAwait(true);
            }

            var request = new ManualBackupRequest(BackupDirectory);
            ManualBackupResult result = await _triggerBackup.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            if (result.Succeeded)
            {
                UpdateLastSuccessfulBackup(result.LastSuccessfulBackup);
                Message = $"Backup validado y creado exitosamente: {result.BackupFilePath}";
                return;
            }

            Message = $"Error al crear backup: {result.Message}";
        }
        catch (Exception ex)
        {
            Message = $"Error inesperado al crear backup: {ex.Message}";
        }
    }

    /// <summary>
    /// Restores the live database from <paramref name="sourceFilePath"/>. Returns true on success so
    /// the view can restart the app; the data replacement and pre-restore snapshot run in infrastructure.
    /// </summary>
    public async Task<bool> RestoreAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (_restoreBackup is null)
        {
            Message = "La restauración no está disponible en este entorno.";
            return false;
        }

        try
        {
            RestoreBackupResult result = await _restoreBackup
                .ExecuteAsync(new RestoreBackupRequest(sourceFilePath), cancellationToken)
                .ConfigureAwait(true);

            Message = result.Succeeded ? result.Message : $"Error al restaurar: {result.Message}";
            return result.Succeeded;
        }
        catch (Exception ex)
        {
            Message = $"Error inesperado al restaurar: {ex.Message}";
            return false;
        }
    }

    private void UpdateLastSuccessfulBackup(ManualBackupMetadata? metadata)
    {
        _lastSuccessfulBackup = metadata;
        OnPropertyChanged(nameof(LastBackupSummary));
        OnPropertyChanged(nameof(LastBackupTimestampUtc));
        OnPropertyChanged(nameof(LastBackupSize));
        OnPropertyChanged(nameof(LastBackupLocation));
        OnPropertyChanged(nameof(LastBackupOperator));
    }
}
