using POS.Application.DTOs.Settings;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class LoadSettingsUseCase
{
    private readonly ISettingsRepository _settings;

    public LoadSettingsUseCase(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<SettingsResult?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings is null ? null : ToResult(settings);
    }

    private static SettingsResult ToResult(BusinessSettings settings) => new(
        settings.Id,
        settings.BusinessName,
        settings.BusinessAddress,
        settings.TaxId,
        settings.BusinessPhone,
        settings.SmtpHost,
        settings.SmtpPort,
        settings.SmtpUser,
        settings.SmtpPassword,
        settings.SmtpUseSsl,
        settings.SmtpSenderAddress,
        settings.PrinterCommand,
        settings.PrinterWorkingDirectory,
        settings.BackupDirectory,
        settings.CurrencyCode,
        [],
        settings.LastBackupAtUtc is null || settings.LastBackupSizeBytes is null || settings.LastBackupLocation is null
            ? null
            : new ManualBackupMetadata(
                settings.LastBackupAtUtc.Value,
                settings.LastBackupSizeBytes.Value,
                settings.LastBackupLocation,
                settings.LastBackupUsername));
}
