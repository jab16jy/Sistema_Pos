namespace POS.Application.DTOs.Settings;

public sealed record SettingsResult(
    Guid Id,
    string BusinessName,
    string? BusinessAddress,
    string? TaxId,
    string? BusinessPhone,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,
    bool SmtpUseSsl,
    string? SmtpSenderAddress,
    string? PrinterCommand,
    string? PrinterWorkingDirectory,
    string? BackupDirectory,
    string CurrencyCode,
    IReadOnlyList<SettingsWarning>? Warnings = null);

public sealed record SaveSettingsRequest(
    string BusinessName,
    string? BusinessAddress,
    string? TaxId,
    string? BusinessPhone,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,
    bool SmtpUseSsl,
    string? SmtpSenderAddress,
    string? PrinterCommand,
    string? PrinterWorkingDirectory,
    string? BackupDirectory,
    string CurrencyCode);

public sealed record SettingsWarning(string Code, string Message);

public sealed record ManualBackupRequest(string? DestinationDirectory = null);

public sealed record ManualBackupResult(bool Succeeded, string Message, string? BackupFilePath);
