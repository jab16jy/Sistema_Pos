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

    public async Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceBackupPath) || !File.Exists(sourceBackupPath))
        {
            throw new FileNotFoundException("Backup file was not found.", sourceBackupPath);
        }

        // Refuse to overwrite the live DB with a file that isn't a valid SQLite database.
        await ValidateBackupAsync(sourceBackupPath, cancellationToken).ConfigureAwait(false);

        string livePath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(livePath) || !File.Exists(livePath))
        {
            throw new FileNotFoundException("Live SQLite database file was not found.", livePath);
        }

        // Safety snapshot: restore overwrites all current data, so keep a recoverable copy of the
        // live DB (taken via the backup API so WAL contents are checkpointed) before touching it.
        string safetyPath = Path.Combine(
            Path.GetDirectoryName(livePath)!,
            $"pos-prerestore-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.db");
        await CopyDatabaseAsync(_connectionString, safetyPath, cancellationToken).ConfigureAwait(false);

        // Release pooled EF connections so the live file isn't locked, then copy the backup's pages
        // into the live database. The caller must restart the app so EF re-reads the restored data.
        SqliteConnection.ClearAllPools();
        await CopyDatabaseAsync($"Data Source={sourceBackupPath};Mode=ReadOnly", livePath, cancellationToken).ConfigureAwait(false);
    }

    // Copies one SQLite database into another using SQLite's online backup API, which handles
    // page-level copying and locking safely even while the source has open connections.
    private static async Task CopyDatabaseAsync(string sourceConnectionString, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection(sourceConnectionString);
        await using var destination = new SqliteConnection($"Data Source={destinationPath};Mode=ReadWriteCreate");
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        source.BackupDatabase(destination);
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
