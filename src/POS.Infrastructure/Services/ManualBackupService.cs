using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class ManualBackupService : IManualBackupService
{
    private readonly string _connectionString;

    public ManualBackupService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> CreateBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("Backup directory is not configured.");
        }

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("Backup directory does not exist.");
        }

        string sourcePath = ResolveDataSource(_connectionString);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("SQLite database file was not found.", sourcePath);
        }

        string fileName = $"pos-backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.db";
        string destinationPath = Path.Combine(destinationDirectory, fileName);

        await using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using FileStream destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        return destinationPath;
    }

    private static string ResolveDataSource(string connectionString)
    {
        const string dataSourceKey = "Data Source=";
        foreach (string segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.StartsWith(dataSourceKey, StringComparison.OrdinalIgnoreCase))
            {
                return segment[dataSourceKey.Length..].Trim().Trim('"');
            }
        }

        return connectionString;
    }
}
