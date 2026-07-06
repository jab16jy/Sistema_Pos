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

    // Impresión y envíos subtab — presentation-only fields. There is no Application-level use case to
    // read/write printer receipt template, SMTP host/port/sender, or WhatsApp script path/working-directory
    // (RuntimeOptions for those live in Infrastructure and are wired once at app startup in App.xaml.cs;
    // exposing a read/write path for them would require a new settings use case, out of scope for this
    // Presentation-only slice). These fields hold local session state so the subtab renders and can drive
    // the real Test-Email/Test-WhatsApp actions below; they are NOT persisted by SaveCommand. Documented in
    // apply-progress as a deviation from the wireframe's fully-editable mock.
    private string _receiptTemplate = "80 mm térmico";
    private string _smtpHost = "smtp.office365.com";
    private string _smtpSenderAddress = "ventas@estheticpos.local";
    private string _smtpPort = "587";
    private string _testEmailRecipient = string.Empty;
    private string _whatsAppScriptPath = "C:\\scripts\\send-whatsapp.ps1";
    private string _whatsAppWorkingDirectory = "C:\\scripts";
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

        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        BackupCommand = new RelayCommand(_ => _ = BackupAsync());
        TestEmailCommand = new RelayCommand(_ => _ = TestEmailAsync());
        TestWhatsAppCommand = new RelayCommand(_ => _ = TestWhatsAppAsync());
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

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
    public string ReceiptTemplate
    {
        get => _receiptTemplate;
        set => SetProperty(ref _receiptTemplate, value ?? "80 mm térmico");
    }

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
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

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
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

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
    public string SmtpPort
    {
        get => _smtpPort;
        set => SetProperty(ref _smtpPort, value ?? "587");
    }

    /// <summary>Destination address for the "Enviar prueba" test-send action (not the actual SMTP account).</summary>
    public string TestEmailRecipient
    {
        get => _testEmailRecipient;
        set => SetProperty(ref _testEmailRecipient, value ?? string.Empty);
    }

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
    public string WhatsAppScriptPath
    {
        get => _whatsAppScriptPath;
        set
        {
            if (SetProperty(ref _whatsAppScriptPath, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsWhatsAppReady));
                OnPropertyChanged(nameof(ReadyChannelsSummary));
            }
        }
    }

    /// <summary>Presentation-only, not persisted — see class remarks.</summary>
    public string WhatsAppWorkingDirectory
    {
        get => _whatsAppWorkingDirectory;
        set => SetProperty(ref _whatsAppWorkingDirectory, value ?? string.Empty);
    }

    /// <summary>Presentation-only status pill value ("Listo" / "Requiere revisión").</summary>
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

    /// <summary>Destination phone number for the "Probar script" test action.</summary>
    public string TestWhatsAppNumber
    {
        get => _testWhatsAppNumber;
        set => SetProperty(ref _testWhatsAppNumber, value ?? string.Empty);
    }

    public bool IsPrinterConfigured => !string.IsNullOrWhiteSpace(PrinterCommand);

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(SmtpSenderAddress);

    public bool IsWhatsAppReady => WhatsAppStatus == "Listo" && !string.IsNullOrWhiteSpace(WhatsAppScriptPath);

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
            Message = $"Error al enviar el correo de prueba: {ex.Message}";
        }
    }

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
            Message = "Ingresa un número para probar el script.";
            return;
        }

        try
        {
            bool sent = await _whatsAppService.SendAsync(
                TestWhatsAppNumber,
                "Prueba de configuración de Esthetic POS.",
                null,
                cancellationToken).ConfigureAwait(true);

            Message = sent ? "Script de WhatsApp ejecutado correctamente." : "El script de WhatsApp no se ejecutó correctamente.";
        }
        catch (Exception ex)
        {
            Message = $"No se pudo probar el script de WhatsApp: {ex.Message}";
        }
    }
}
