namespace POS.Domain.Entities;

public sealed class BusinessSettings
{
    private BusinessSettings()
    {
        BusinessName = string.Empty;
        CurrencyCode = "COP";
    }

    public BusinessSettings(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? backupDirectory,
        string currencyCode)
    {
        Id = Guid.NewGuid();
        Apply(
            businessName,
            businessAddress,
            taxId,
            smtpHost,
            smtpPort,
            smtpUser,
            smtpPassword,
            smtpUseSsl,
            smtpSenderAddress,
            printerCommand,
            printerWorkingDirectory,
            backupDirectory,
            currencyCode);
    }

    public Guid Id { get; private set; }
    public string BusinessName { get; private set; } = string.Empty;
    public string? BusinessAddress { get; private set; }
    public string? TaxId { get; private set; }
    public string? SmtpHost { get; private set; }
    public int? SmtpPort { get; private set; }
    public string? SmtpUser { get; private set; }
    public string? SmtpPassword { get; private set; }
    public bool SmtpUseSsl { get; private set; }
    public string? SmtpSenderAddress { get; private set; }
    public string? PrinterCommand { get; private set; }
    public string? PrinterWorkingDirectory { get; private set; }
    public string? BackupDirectory { get; private set; }
    public string CurrencyCode { get; private set; } = "COP";

    public void Update(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? backupDirectory,
        string currencyCode)
    {
        Apply(
            businessName,
            businessAddress,
            taxId,
            smtpHost,
            smtpPort,
            smtpUser,
            smtpPassword,
            smtpUseSsl,
            smtpSenderAddress,
            printerCommand,
            printerWorkingDirectory,
            backupDirectory,
            currencyCode);
    }

    private void Apply(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? backupDirectory,
        string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new ArgumentException("Business name is required.", nameof(businessName));
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        string normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException("Currency code must be a 3-letter ISO code.", nameof(currencyCode));
        }

        if (smtpPort is <= 0)
        {
            throw new ArgumentException("SMTP port must be greater than zero when provided.", nameof(smtpPort));
        }

        BusinessName = businessName.Trim();
        BusinessAddress = TrimToNull(businessAddress);
        TaxId = TrimToNull(taxId);
        SmtpHost = TrimToNull(smtpHost);
        SmtpPort = smtpPort;
        SmtpUser = TrimToNull(smtpUser);
        SmtpPassword = TrimToNull(smtpPassword);
        SmtpUseSsl = smtpUseSsl;
        SmtpSenderAddress = TrimToNull(smtpSenderAddress);
        PrinterCommand = TrimToNull(printerCommand);
        PrinterWorkingDirectory = TrimToNull(printerWorkingDirectory);
        BackupDirectory = TrimToNull(backupDirectory);
        CurrencyCode = normalizedCurrency;
    }

    private static string? TrimToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
