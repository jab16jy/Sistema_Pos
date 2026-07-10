using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class RestoreBackupUseCase
{
    private readonly IManualBackupService _backupService;

    public RestoreBackupUseCase(IManualBackupService backupService)
    {
        _backupService = backupService;
    }

    public async Task<RestoreBackupResult> ExecuteAsync(
        RestoreBackupRequest request,
        CancellationToken cancellationToken = default)
    {
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
