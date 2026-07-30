namespace POS.Domain.Entities;

public sealed class BusinessSettings
{
    public const decimal DefaultMaximumCashierDiscountPercent = 20m;
    public const decimal DefaultMaximumDeferredBalancePercent = 50m;
    public const decimal DefaultMaximumPointsMultiplier = 5m;

    private BusinessSettings()
    {
        BusinessName = string.Empty;
        CurrencyCode = "COP";
        MaximumCashierDiscountPercent = DefaultMaximumCashierDiscountPercent;
        MaximumDeferredBalancePercent = DefaultMaximumDeferredBalancePercent;
        MaximumPointsMultiplier = DefaultMaximumPointsMultiplier;
    }

    public BusinessSettings(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? businessPhone,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? printerPort,
        string? backupDirectory,
        string currencyCode,
        decimal maximumCashierDiscountPercent = DefaultMaximumCashierDiscountPercent,
        decimal maximumDeferredBalancePercent = DefaultMaximumDeferredBalancePercent,
        decimal maximumPointsMultiplier = DefaultMaximumPointsMultiplier)
    {
        Id = Guid.NewGuid();
        Apply(
            businessName,
            businessAddress,
            taxId,
            businessPhone,
            smtpHost,
            smtpPort,
            smtpUser,
            smtpPassword,
            smtpUseSsl,
            smtpSenderAddress,
            printerCommand,
            printerWorkingDirectory,
            printerPort,
            backupDirectory,
            currencyCode,
            maximumCashierDiscountPercent,
            maximumDeferredBalancePercent,
            maximumPointsMultiplier);
    }

    public Guid Id { get; private set; }
    public string BusinessName { get; private set; } = string.Empty;
    public string? BusinessAddress { get; private set; }
    public string? TaxId { get; private set; }
    public string? BusinessPhone { get; private set; }
    public string? SmtpHost { get; private set; }
    public int? SmtpPort { get; private set; }
    public string? SmtpUser { get; private set; }
    public string? SmtpPassword { get; private set; }
    public bool SmtpUseSsl { get; private set; }
    public string? SmtpSenderAddress { get; private set; }
    public string? PrinterCommand { get; private set; }
    public string? PrinterWorkingDirectory { get; private set; }
    public string? PrinterPort { get; private set; }
    public string? BackupDirectory { get; private set; }
    public DateTimeOffset? LastBackupAtUtc { get; private set; }
    public long? LastBackupSizeBytes { get; private set; }
    public string? LastBackupLocation { get; private set; }
    public string? LastBackupUsername { get; private set; }
    public string CurrencyCode { get; private set; } = "COP";
    public decimal MaximumCashierDiscountPercent { get; private set; } = DefaultMaximumCashierDiscountPercent;
    public decimal MaximumDeferredBalancePercent { get; private set; } = DefaultMaximumDeferredBalancePercent;

    /// <summary>Upper bound for the points multiplier a loyalty tier may grant.</summary>
    public decimal MaximumPointsMultiplier { get; private set; } = DefaultMaximumPointsMultiplier;

    public void Update(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? businessPhone,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? printerPort,
        string? backupDirectory,
        string currencyCode,
        decimal maximumCashierDiscountPercent = DefaultMaximumCashierDiscountPercent,
        decimal maximumDeferredBalancePercent = DefaultMaximumDeferredBalancePercent,
        decimal maximumPointsMultiplier = DefaultMaximumPointsMultiplier)
    {
        Apply(
            businessName,
            businessAddress,
            taxId,
            businessPhone,
            smtpHost,
            smtpPort,
            smtpUser,
            smtpPassword,
            smtpUseSsl,
            smtpSenderAddress,
            printerCommand,
            printerWorkingDirectory,
            printerPort,
            backupDirectory,
            currencyCode,
            maximumCashierDiscountPercent,
            maximumDeferredBalancePercent,
            maximumPointsMultiplier);
    }

    public void RecordSuccessfulBackup(
        DateTimeOffset timestampUtc,
        long sizeBytes,
        string location,
        string? username)
    {
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Backup size must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Backup location is required.", nameof(location));
        }

        LastBackupAtUtc = timestampUtc.ToUniversalTime();
        LastBackupSizeBytes = sizeBytes;
        LastBackupLocation = location.Trim();
        LastBackupUsername = TrimToNull(username);
    }

    private void Apply(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? businessPhone,
        string? smtpHost,
        int? smtpPort,
        string? smtpUser,
        string? smtpPassword,
        bool smtpUseSsl,
        string? smtpSenderAddress,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? printerPort,
        string? backupDirectory,
        string currencyCode,
        decimal maximumCashierDiscountPercent,
        decimal maximumDeferredBalancePercent,
        decimal maximumPointsMultiplier)
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

        if (maximumCashierDiscountPercent < 0m || maximumCashierDiscountPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCashierDiscountPercent),
                "Maximum cashier discount must be between 0 and 100 percent.");
        }

        if (maximumDeferredBalancePercent < 0m || maximumDeferredBalancePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDeferredBalancePercent),
                "Maximum deferred balance must be between 0 and 100 percent.");
        }

        // Lower bound is 1: a cap below 1 would reject every tier, since a tier multiplier of 1
        // means "no bonus" and is the neutral value tiers default to.
        if (maximumPointsMultiplier < 1m || maximumPointsMultiplier > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPointsMultiplier),
                "Maximum points multiplier must be between 1 and 100.");
        }

        BusinessName = businessName.Trim();
        BusinessAddress = TrimToNull(businessAddress);
        TaxId = TrimToNull(taxId);
        BusinessPhone = TrimToNull(businessPhone);
        SmtpHost = TrimToNull(smtpHost);
        SmtpPort = smtpPort;
        SmtpUser = TrimToNull(smtpUser);
        SmtpPassword = TrimToNull(smtpPassword);
        SmtpUseSsl = smtpUseSsl;
        SmtpSenderAddress = TrimToNull(smtpSenderAddress);
        PrinterCommand = TrimToNull(printerCommand);
        PrinterWorkingDirectory = TrimToNull(printerWorkingDirectory);
        PrinterPort = TrimToNull(printerPort);
        BackupDirectory = TrimToNull(backupDirectory);
        CurrencyCode = normalizedCurrency;
        MaximumCashierDiscountPercent = maximumCashierDiscountPercent;
        MaximumDeferredBalancePercent = maximumDeferredBalancePercent;
        MaximumPointsMultiplier = maximumPointsMultiplier;
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
