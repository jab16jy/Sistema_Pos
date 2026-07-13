using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;

namespace POS.Application.UseCases.Settings;

public sealed class RestoreBackupUseCase
{
    private readonly IManualBackupService _backupService;
    private readonly ICurrentOperatorSession _session;

    public RestoreBackupUseCase(IManualBackupService backupService, ICurrentOperatorSession session)
    {
        _backupService = backupService;
        _session = session;
    }

    public async Task<RestoreBackupResult> ExecuteAsync(
        RestoreBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        string? unauthorized = PermissionAuthorization.EnsureHasPermission(
            _session, PermissionKeys.Admin.Backup.Restore, "No tienes permiso para restaurar respaldos.");
        if (unauthorized is not null)
        {
            return new RestoreBackupResult(false, unauthorized);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.SourceFilePath))
        {
            return new RestoreBackupResult(false, "Selecciona un archivo de respaldo válido.");
        }

        try
        {
            await _backupService.RestoreBackupAsync(request.SourceFilePath, cancellationToken).ConfigureAwait(false);
            return new RestoreBackupResult(true, "Restauración completada. La aplicación se reiniciará.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new RestoreBackupResult(false, ex.Message);
        }
    }
}
