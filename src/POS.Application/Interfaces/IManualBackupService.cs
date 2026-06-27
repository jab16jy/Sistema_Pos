namespace POS.Application.Interfaces;

public interface IManualBackupService
{
    Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default);
}
