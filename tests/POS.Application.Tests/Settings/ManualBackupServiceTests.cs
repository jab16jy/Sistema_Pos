using System.IO;
using Microsoft.Data.Sqlite;
using POS.Application.DTOs.Settings;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests.Settings;

public sealed class ManualBackupServiceTests
{
    [Fact]
    public async Task RestoreBackupAsync_RestoresPreviousData_AndKeepsPreRestoreSnapshot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pos-restore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string livePath = Path.Combine(dir, "live.db");
        string connectionString = $"Data Source={livePath}";
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await SeedAsync(connectionString, "original", ct);

            var service = new ManualBackupService(connectionString);
            BackupFile backup = await service.CreateBackupAsync(dir, ct);

            await SeedAsync(connectionString, "changed", ct);
            Assert.Equal("changed", await ReadAsync(connectionString, ct));

            await service.RestoreBackupAsync(backup.Location, ct);

            Assert.Equal("original", await ReadAsync(connectionString, ct));
            Assert.Contains(Directory.GetFiles(dir), f => Path.GetFileName(f).StartsWith("pos-prerestore-"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_WhenSourceMissing_Throws()
    {
        var service = new ManualBackupService($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db")}");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.RestoreBackupAsync(
                Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".db"),
                TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(string connectionString, string value, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS t (v TEXT); DELETE FROM t; INSERT INTO t (v) VALUES ($v);";
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string?> ReadAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT v FROM t LIMIT 1;";
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }
}
