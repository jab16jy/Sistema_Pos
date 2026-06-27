using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Settings;
using POS.Application.UseCases.Settings;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly LoadSettingsUseCase _loadSettings;
    private readonly SaveSettingsUseCase _saveSettings;
    private readonly TriggerManualBackupUseCase _triggerBackup;
    private string _businessName = string.Empty;
    private string? _businessAddress;
    private string? _taxId;
    private string? _printerCommand;
    private string? _printerWorkingDirectory;
    private string? _backupDirectory;
    private string _currencyCode = "COP";
    private string _message = "Carga la configuración del negocio o realiza un backup.";

    public SettingsViewModel(
        LoadSettingsUseCase loadSettings,
        SaveSettingsUseCase saveSettings,
        TriggerManualBackupUseCase triggerBackup)
    {
        _loadSettings = loadSettings;
        _saveSettings = saveSettings;
        _triggerBackup = triggerBackup;
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        BackupCommand = new RelayCommand(_ => _ = BackupAsync());
    }

    public ObservableCollection<SettingsWarning> Warnings { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand BackupCommand { get; }

    public string BusinessName
    {
        get => _businessName;
        set => SetProperty(ref _businessName, value);
    }

    public string? BusinessAddress
    {
        get => _businessAddress;
        set => SetProperty(ref _businessAddress, value);
    }

    public string? TaxId
    {
        get => _taxId;
        set => SetProperty(ref _taxId, value);
    }

    public string? PrinterCommand
    {
        get => _printerCommand;
        set => SetProperty(ref _printerCommand, value);
    }

    public string? PrinterWorkingDirectory
    {
        get => _printerWorkingDirectory;
        set => SetProperty(ref _printerWorkingDirectory, value);
    }

    public string? BackupDirectory
    {
        get => _backupDirectory;
        set => SetProperty(ref _backupDirectory, value);
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set => SetProperty(ref _currencyCode, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        SettingsResult? settings = await _loadSettings.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        if (settings is null)
        {
            Message = "No hay configuración guardada. Ingresa los datos del negocio y guarda.";
            return;
        }

        BusinessName = settings.BusinessName;
        BusinessAddress = settings.BusinessAddress;
        TaxId = settings.TaxId;
        PrinterCommand = settings.PrinterCommand;
        PrinterWorkingDirectory = settings.PrinterWorkingDirectory;
        BackupDirectory = settings.BackupDirectory;
        CurrencyCode = settings.CurrencyCode;

        Warnings.Clear();
        if (settings.Warnings is not null)
        {
            foreach (var warning in settings.Warnings)
            {
                Warnings.Add(warning);
            }
        }

        Message = Warnings.Count == 0
            ? "Configuración cargada exitosamente."
            : $"Configuración cargada con {Warnings.Count} advertencia(s).";
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SaveSettingsRequest(
                BusinessName,
                BusinessAddress,
                TaxId,
                PrinterCommand,
                PrinterWorkingDirectory,
                BackupDirectory,
                CurrencyCode);

            SettingsResult result = await _saveSettings.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            Warnings.Clear();
            if (result.Warnings is not null)
            {
                foreach (var warning in result.Warnings)
                {
                    Warnings.Add(warning);
                }
            }

            Message = Warnings.Count == 0
                ? "Configuración guardada exitosamente."
                : $"Configuración guardada con {Warnings.Count} advertencia(s). Revisa la sección de impresora.";
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task BackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ManualBackupRequest(BackupDirectory);
            ManualBackupResult result = await _triggerBackup.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            Message = result.Succeeded
                ? $"Backup creado exitosamente: {result.BackupFilePath}"
                : $"Error al crear backup: {result.Message}";
        }
        catch (Exception ex)
        {
            Message = $"Error inesperado al crear backup: {ex.Message}";
        }
    }
}
