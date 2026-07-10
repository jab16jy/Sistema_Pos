using POS.Application.DTOs.Settings;

namespace POS.Application.Interfaces;

public interface IManualBackupService
{
    Task<BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default);
}
