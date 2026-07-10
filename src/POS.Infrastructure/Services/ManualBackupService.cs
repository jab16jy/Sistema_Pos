using Microsoft.Data.Sqlite;
using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class ManualBackupService : IManualBackupService
{
    private readonly string _connectionString;

    public ManualBackupService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<BackupFile> CreateBackupAsync(
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

        string sourcePath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("SQLite database file was not found.", sourcePath);
        }

        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
        string fileName = $"pos-backup-{createdAtUtc:yyyyMMddTHHmmssfffZ}.db";
        string destinationPath = Path.Combine(destinationDirectory, fileName);

        try
        {
            await using (var source = new SqliteConnection(_connectionString))
            await using (var destination = new SqliteConnection($"Data Source={destinationPath};Mode=ReadWriteCreate"))
            {
                await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                source.BackupDatabase(destination);
            }

            await ValidateBackupAsync(destinationPath, cancellationToken).ConfigureAwait(false);

            long sizeBytes = new FileInfo(destinationPath).Length;
            if (sizeBytes <= 0)
            {
                throw new InvalidDataException("SQLite backup file is empty.");
            }

            return new BackupFile(destinationPath, createdAtUtc, sizeBytes);
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    private static async Task ValidateBackupAsync(string destinationPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={destinationPath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";

        string? result = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("SQLite backup integrity validation failed.");
        }
    }
}
