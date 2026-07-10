using Microsoft.Data.Sqlite;
using POS.Application.DTOs.Settings;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests.Settings;

public sealed class ManualBackupServiceIntegrationTests
{
    [Fact]
    public async Task CreateBackupAsync_WithWalDatabase_CreatesValidatedOnlineSnapshot()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"pos-backup-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(tempDirectory, "pos.db");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string sourceConnectionString = $"Data Source={sourcePath}";
            await using (var source = new SqliteConnection(sourceConnectionString))
            {
                await source.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = source.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE BackupProbe (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); INSERT INTO BackupProbe (Name) VALUES ('consistent');";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var service = new ManualBackupService(sourceConnectionString);
            BackupFile backup = await service.CreateBackupAsync(tempDirectory, TestContext.Current.CancellationToken);

            Assert.Matches(@"^pos-backup-\d{8}T\d{9}Z\.db$", Path.GetFileName(backup.Location));
            Assert.Equal(TimeSpan.Zero, backup.CreatedAtUtc.Offset);
            Assert.True(backup.SizeBytes > 0);
            Assert.True(File.Exists(backup.Location));

            await using var snapshot = new SqliteConnection($"Data Source={backup.Location};Mode=ReadOnly");
            await snapshot.OpenAsync(TestContext.Current.CancellationToken);
            await using var countCommand = snapshot.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM BackupProbe WHERE Name = 'consistent';";
            Assert.Equal(1L, (long)(await countCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);

            await using var integrityCommand = snapshot.CreateCommand();
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", await integrityCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
