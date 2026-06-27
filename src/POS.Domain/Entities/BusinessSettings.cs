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
            printerCommand,
            printerWorkingDirectory,
            backupDirectory,
            currencyCode);
    }

    public Guid Id { get; private set; }
    public string BusinessName { get; private set; } = string.Empty;
    public string? BusinessAddress { get; private set; }
    public string? TaxId { get; private set; }
    public string? PrinterCommand { get; private set; }
    public string? PrinterWorkingDirectory { get; private set; }
    public string? BackupDirectory { get; private set; }
    public string CurrencyCode { get; private set; } = "COP";

    public void Update(
        string businessName,
        string? businessAddress,
        string? taxId,
        string? printerCommand,
        string? printerWorkingDirectory,
        string? backupDirectory,
        string currencyCode)
    {
        Apply(
            businessName,
            businessAddress,
            taxId,
            printerCommand,
            printerWorkingDirectory,
            backupDirectory,
            currencyCode);
    }

    private void Apply(
        string businessName,
        string? businessAddress,
        string? taxId,
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

        BusinessName = businessName.Trim();
        BusinessAddress = TrimToNull(businessAddress);
        TaxId = TrimToNull(taxId);
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
