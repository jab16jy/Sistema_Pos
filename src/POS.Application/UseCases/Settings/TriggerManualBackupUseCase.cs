using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class TriggerManualBackupUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IManualBackupService _backupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentOperatorSession _session;

    public TriggerManualBackupUseCase(
        ISettingsRepository settings,
        IManualBackupService backupService,
        IUnitOfWork unitOfWork,
        ICurrentOperatorSession session)
    {
        _settings = settings;
        _backupService = backupService;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    public async Task<ManualBackupResult> ExecuteAsync(
        ManualBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        string? destinationDirectory = Normalize(request.DestinationDirectory) ?? Normalize(settings?.BackupDirectory);

        if (destinationDirectory is null)
        {
            return new ManualBackupResult(false, "Backup directory is not configured.", null);
        }

        if (settings is null)
        {
            return new ManualBackupResult(false, "Business settings must be saved before creating a backup.", null);
        }

        try
        {
            BackupFile backupFile = await _backupService
                .CreateBackupAsync(destinationDirectory, cancellationToken)
                .ConfigureAwait(false);

            var metadata = new ManualBackupMetadata(
                backupFile.CreatedAtUtc.ToUniversalTime(),
                backupFile.SizeBytes,
                backupFile.Location,
                _session.IsAuthenticated ? _session.Username : null);

            settings.RecordSuccessfulBackup(
                metadata.TimestampUtc,
                metadata.SizeBytes,
                metadata.Location,
                metadata.OperatorUsername);
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ManualBackupResult(true, "Backup created and validated successfully.", backupFile.Location, metadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
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
