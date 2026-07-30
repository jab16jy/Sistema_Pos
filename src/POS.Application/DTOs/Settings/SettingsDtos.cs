using POS.Domain.Entities;

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
    string? PrinterPort,
    string? BackupDirectory,
    string CurrencyCode,
    IReadOnlyList<SettingsWarning>? Warnings = null,
    ManualBackupMetadata? LastSuccessfulBackup = null,
    decimal MaximumCashierDiscountPercent = BusinessSettings.DefaultMaximumCashierDiscountPercent,
    decimal MaximumDeferredBalancePercent = BusinessSettings.DefaultMaximumDeferredBalancePercent,
    decimal MaximumPointsMultiplier = BusinessSettings.DefaultMaximumPointsMultiplier);

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
    string? PrinterPort,
    string? BackupDirectory,
    string CurrencyCode,
    decimal MaximumCashierDiscountPercent = BusinessSettings.DefaultMaximumCashierDiscountPercent,
    decimal MaximumDeferredBalancePercent = BusinessSettings.DefaultMaximumDeferredBalancePercent,
    decimal MaximumPointsMultiplier = BusinessSettings.DefaultMaximumPointsMultiplier);

public sealed record SettingsWarning(string Code, string Message);

public sealed record ManualBackupRequest(string? DestinationDirectory = null);

public sealed record BackupFile(string Location, DateTimeOffset CreatedAtUtc, long SizeBytes);

public sealed record ManualBackupMetadata(
    DateTimeOffset TimestampUtc,
    long SizeBytes,
    string Location,
    string? OperatorUsername);

public sealed record ManualBackupResult(
    bool Succeeded,
    string Message,
    string? BackupFilePath,
    ManualBackupMetadata? LastSuccessfulBackup = null);

public sealed record RestoreBackupRequest(string SourceFilePath);

public sealed record RestoreBackupResult(bool Succeeded, string Message);
