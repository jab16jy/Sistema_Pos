using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly LoadSettingsUseCase _loadSettings;
    private readonly SaveSettingsUseCase _saveSettings;
    private readonly TriggerManualBackupUseCase _triggerBackup;
    private readonly IEmailService? _emailService;
    private readonly IWhatsAppService? _whatsAppService;
    private readonly IInstalledPrinterProvider? _printerProvider;

    // Virtual/system printers that are never a receipt target — excluded from auto-detection
    // (still listed, so the user can pick one manually if they ever need to).
    private static readonly string[] VirtualPrinterHints =
        ["XPS", "PDF", "OneNote", "Fax", "Microsoft Print"];

    private string _businessName = string.Empty;
    private string? _businessAddress;
    private string? _taxId;
    private string? _printerCommand;
    private string? _printerWorkingDirectory;
    private string? _backupDirectory;
    private string _currencyCode = "COP";
    private string _message = "Carga la configuración del negocio o realiza un backup.";

    private string _receiptTemplate = "80 mm térmico";
    private string _smtpHost = string.Empty;
    private string _smtpSenderAddress = string.Empty;
    private string _smtpUser = string.Empty;
    private string _smtpPassword = string.Empty;
    private string _smtpPort = "587";
    private bool _smtpUseSsl = true;
    private string _testEmailRecipient = string.Empty;
    private string _whatsAppStatus = "Listo";
    private string _testWhatsAppNumber = string.Empty;

    public SettingsViewModel(
        LoadSettingsUseCase loadSettings,
        SaveSettingsUseCase saveSettings,
        TriggerManualBackupUseCase triggerBackup,
        IEmailService? emailService = null,
        IWhatsAppService? whatsAppService = null,
        IInstalledPrinterProvider? printerProvider = null)
    {
        _loadSettings = loadSettings;
        _saveSettings = saveSettings;
        _triggerBackup = triggerBackup;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _printerProvider = printerProvider;

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar configuración: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar configuración: {ex.Message}"));
        BackupCommand = new RelayCommand(_ => RunSafe(BackupAsync(), ex => Message = $"Error inesperado al crear backup: {ex.Message}"));
        TestEmailCommand = new RelayCommand(_ => RunSafe(TestEmailAsync(), ex => Message = $"Error al enviar el correo de prueba: {GetSafeEmailErrorMessage(ex)}"));
        TestWhatsAppCommand = new RelayCommand(_ => RunSafe(TestWhatsAppAsync(), ex => Message = $"No se pudo abrir WhatsApp: {ex.Message}"));
        RefreshPrintersCommand = new RelayCommand(_ => RefreshPrinters());

        RefreshPrinters();
    }

    public ObservableCollection<SettingsWarning> Warnings { get; } = [];

    /// <summary>Installed printers offered in the Configuración dropdown (auto-detected from the OS).</summary>
    public ObservableCollection<string> AvailablePrinters { get; } = [];

    public ICommand RefreshPrintersCommand { get; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand BackupCommand { get; }

    public ICommand TestEmailCommand { get; }

    public ICommand TestWhatsAppCommand { get; }

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
        set
        {
            if (SetProperty(ref _printerCommand, value))
            {
                OnPropertyChanged(nameof(IsPrinterConfigured));
                OnPropertyChanged(nameof(ReadyChannelsSummary));
            }
        }
    }

    public string? PrinterWorkingDirectory
    {
        get => _printerWorkingDirectory;
        set => SetProperty(ref _printerWorkingDirectory, value);
    }

    public string? BackupDirectory
    {
        get => _backupDirectory;
        set
        {
            if (SetProperty(ref _backupDirectory, value))
            {
                OnPropertyChanged(nameof(IsBackupConfigured));
                OnPropertyChanged(nameof(LastBackupSummary));
            }
        }
    }

    /// <summary>
    /// "Usuarios activos" KPI. No aggregate use case exposes an active-user count to Presentation today
    /// (out of scope to add one in this slice) — shown as an explicit "sin datos" placeholder rather than
    /// a fabricated number.
    /// </summary>
    public string ActiveUsersSummary => "—";

    /// <summary>"Turno actual" KPI — see <see cref="ActiveUsersSummary"/> remarks; no shift-status query is wired here.</summary>
    public string CurrentShiftSummary => "—";

    /// <summary>"Último backup" KPI: reflects whether a backup directory is configured, not an actual last-run timestamp (no query for that exists).</summary>
    public string LastBackupSummary => IsBackupConfigured ? "Configurado" : "Sin configurar";

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

    public void ShowError(string message) => Message = message;

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
    public string ReceiptTemplate
    {
        get => _receiptTemplate;
        set => SetProperty(ref _receiptTemplate, value ?? "80 mm térmico");
    }

    public string SmtpHost
    {
        get => _smtpHost;
        set
        {
            if (SetProperty(ref _smtpHost, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsSmtpConfigured));
                OnPropertyChanged(nameof(ReadyChannelsSummary));
            }
        }
    }

    public string SmtpSenderAddress
    {
        get => _smtpSenderAddress;
        set
        {
            if (SetProperty(ref _smtpSenderAddress, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsSmtpConfigured));
                OnPropertyChanged(nameof(ReadyChannelsSummary));
            }
        }
    }

    public string SmtpUser
    {
        get => _smtpUser;
        set => SetProperty(ref _smtpUser, value ?? string.Empty);
    }

    public string SmtpPassword
    {
        get => _smtpPassword;
        set => SetProperty(ref _smtpPassword, value ?? string.Empty);
    }

    public string SmtpPort
    {
        get => _smtpPort;
        set => SetProperty(ref _smtpPort, value ?? "587");
    }

    public bool SmtpUseSsl
    {
        get => _smtpUseSsl;
        set => SetProperty(ref _smtpUseSsl, value);
    }

    /// <summary>Destination address for the "Enviar prueba" test-send action (not the actual SMTP account).</summary>
    public string TestEmailRecipient
    {
        get => _testEmailRecipient;
        set => SetProperty(ref _testEmailRecipient, value ?? string.Empty);
    }

    /// <summary>Status pill for WhatsApp Desktop/Web integration.</summary>
    public string WhatsAppStatus
    {
        get => _whatsAppStatus;
        set
        {
            if (SetProperty(ref _whatsAppStatus, value ?? "Requiere revisión"))
            {
                OnPropertyChanged(nameof(IsWhatsAppReady));
                OnPropertyChanged(nameof(ReadyChannelsSummary));
            }
        }
    }

    /// <summary>Destination phone number for the WhatsApp Desktop/Web test action.</summary>
    public string TestWhatsAppNumber
    {
        get => _testWhatsAppNumber;
        set => SetProperty(ref _testWhatsAppNumber, value ?? string.Empty);
    }

    public bool IsPrinterConfigured => !string.IsNullOrWhiteSpace(PrinterCommand);

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(SmtpSenderAddress);

    public bool IsWhatsAppReady => WhatsAppStatus == "Listo" && _whatsAppService is not null;

    public bool IsBackupConfigured => !string.IsNullOrWhiteSpace(BackupDirectory);

    /// <summary>"Canales listos" KPI: how many of the 3 real integration channels this view manages are configured.</summary>
    public string ReadyChannelsSummary
    {
        get
        {
            int ready = (IsPrinterConfigured ? 1 : 0) + (IsSmtpConfigured ? 1 : 0) + (IsWhatsAppReady ? 1 : 0);
            return $"{ready} / 3";
        }
    }

    /// <summary>
    /// Re-reads the OS printer list into <see cref="AvailablePrinters"/> and, when no printer is
    /// configured yet, auto-selects the first physical (non-virtual) printer found. Call after the
    /// user reconnects a printer to pick it up without restarting.
    /// </summary>
    public void RefreshPrinters()
    {
        var printers = _printerProvider?.GetInstalledPrinters() ?? [];

        AvailablePrinters.Clear();
        foreach (var name in printers)
        {
            AvailablePrinters.Add(name);
        }

        if (string.IsNullOrWhiteSpace(PrinterCommand))
        {
            string? candidate = PickDefaultPrinter(printers);
            if (candidate is not null)
            {
                PrinterCommand = candidate;
            }
        }
    }

    /// <summary>First physical printer (skips XPS/PDF/OneNote/Fax), or null if only virtual ones exist.</summary>
    public static string? PickDefaultPrinter(IReadOnlyList<string> printers) =>
        printers.FirstOrDefault(p =>
            !VirtualPrinterHints.Any(h => p.Contains(h, StringComparison.OrdinalIgnoreCase)));

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
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
            SmtpHost = settings.SmtpHost ?? string.Empty;
            SmtpPort = settings.SmtpPort?.ToString() ?? "587";
            SmtpUser = settings.SmtpUser ?? string.Empty;
            SmtpPassword = settings.SmtpPassword ?? string.Empty;
            SmtpUseSsl = settings.SmtpUseSsl;
            SmtpSenderAddress = settings.SmtpSenderAddress ?? string.Empty;
            PrinterCommand = settings.PrinterCommand;
            PrinterWorkingDirectory = settings.PrinterWorkingDirectory;

            // No printer saved yet → auto-detect one from the OS so the caja works out of the box.
            if (string.IsNullOrWhiteSpace(PrinterCommand))
            {
                RefreshPrinters();
            }
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
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar configuración: {ex.Message}";
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveCurrentSettingsAsync(cancellationToken).ConfigureAwait(true);
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

    /// <summary>
    /// Sends a test email through the real, DI-configured <see cref="IEmailService"/> to
    /// <see cref="TestEmailRecipient"/>. Reports success/failure without crashing (per spec); never throws.
    /// </summary>
    public async Task TestEmailAsync(CancellationToken cancellationToken = default)
    {
        if (_emailService is null)
        {
            Message = "El envío de correo no está disponible en este entorno.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TestEmailRecipient))
        {
            Message = "Ingresa un destinatario para la prueba de envío.";
            return;
        }

        try
        {
            await SaveCurrentSettingsAsync(cancellationToken).ConfigureAwait(true);

            bool sent = await _emailService.SendAsync(
                TestEmailRecipient,
                "Prueba de configuración — Esthetic POS",
                "Este es un correo de prueba enviado desde Configuración.",
                [],
                "prueba.txt",
                cancellationToken).ConfigureAwait(true);

            Message = sent ? "Correo de prueba enviado correctamente." : "No se pudo enviar el correo de prueba.";
        }
        catch (Exception ex)
        {
            Message = $"Error al enviar el correo de prueba: {GetSafeEmailErrorMessage(ex)}";
        }
    }

    private static string GetSafeEmailErrorMessage(Exception ex) =>
        ex is ArgumentException or InvalidOperationException
            ? ex.Message
            : "No se pudo enviar el correo. Verifica host, puerto, SSL/TLS, usuario, contraseña de aplicación y remitente SMTP.";

    /// <summary>
    /// Runs the real, DI-configured <see cref="IWhatsAppService"/> script against <see cref="TestWhatsAppNumber"/>.
    /// Reports success/failure without crashing (per spec) and never blocks other Configuración actions.
    /// </summary>
    public async Task TestWhatsAppAsync(CancellationToken cancellationToken = default)
    {
        if (_whatsAppService is null)
        {
            Message = "El envío por WhatsApp no está disponible en este entorno.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TestWhatsAppNumber))
        {
            Message = "Ingresa un número para probar WhatsApp.";
            return;
        }

        try
        {
            bool sent = await _whatsAppService.SendAsync(
                TestWhatsAppNumber,
                "Prueba de configuración de Esthetic POS.",
                null,
                cancellationToken).ConfigureAwait(true);

            Message = sent ? "WhatsApp abierto correctamente." : "No se pudo abrir WhatsApp en este equipo.";
        }
        catch (Exception ex)
        {
            Message = $"No se pudo abrir WhatsApp: {ex.Message}";
        }
    }

    private async Task<SettingsResult> SaveCurrentSettingsAsync(CancellationToken cancellationToken)
    {
        int? smtpPort = null;
        if (!string.IsNullOrWhiteSpace(SmtpPort))
        {
            if (!int.TryParse(SmtpPort, out int parsedPort) || parsedPort <= 0)
            {
                throw new ArgumentException("El puerto SMTP debe ser un número mayor que cero.", nameof(SmtpPort));
            }

            smtpPort = parsedPort;
        }

        var request = new SaveSettingsRequest(
            BusinessName,
            BusinessAddress,
            TaxId,
            SmtpHost,
            smtpPort,
            SmtpUser,
            SmtpPassword,
            SmtpUseSsl,
            SmtpSenderAddress,
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

        return result;
    }
}
