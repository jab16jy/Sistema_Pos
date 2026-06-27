using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class TriggerManualBackupUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IManualBackupService _backupService;

    public TriggerManualBackupUseCase(ISettingsRepository settings, IManualBackupService backupService)
    {
        _settings = settings;
        _backupService = backupService;
    }

    public async Task<ManualBackupResult> ExecuteAsync(
        ManualBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        string? destinationDirectory = Normalize(request.DestinationDirectory);

        if (destinationDirectory is null)
        {
            BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
            destinationDirectory = Normalize(settings?.BackupDirectory);
        }

        if (destinationDirectory is null)
        {
            return new ManualBackupResult(false, "Backup directory is not configured.", null);
        }

        try
        {
            string backupFilePath = await _backupService
                .CreateBackupAsync(destinationDirectory, cancellationToken)
                .ConfigureAwait(false);

            return new ManualBackupResult(true, "Backup created successfully", backupFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ManualBackupResult(false, ex.Message, null);
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
