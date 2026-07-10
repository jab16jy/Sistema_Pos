using POS.Application.DTOs.Settings;

namespace POS.Application.Interfaces;

public interface IManualBackupService
{
    Task<BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the live database with the contents of <paramref name="sourceBackupPath"/>.
    /// Validates the source integrity and snapshots the current database before overwriting.
    /// The application must restart afterwards so cached state reflects the restored data.
    /// </summary>
    Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default);
}
