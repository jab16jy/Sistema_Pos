using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Persistence;

// The manual backup tool lives behind the login screen, so a user whose upgrade fails at startup
// can never take one. These tests pin the only window where a snapshot is still possible: the
// rollout takes it itself, before the first pending migration touches the existing data.
public sealed class SchemaRolloutSnapshotTests
{
    private const string LastPreHardeningMigration = "20260715031926_MakeSaleReturnIndexNonUnique";

    [Fact]
    public async Task EnsureLatestSchemaAsync_WithPendingMigrations_SnapshotsTheDatabaseFirst()
    {
        string workingDirectory = CreateWorkingDirectory();
        string dbPath = Path.Combine(workingDirectory, "pos.db");

        try
        {
            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var context = CreateDbContext(connection);
                await context.GetService<IMigrator>().MigrateAsync(LastPreHardeningMigration, TestContext.Current.CancellationToken);
                await connection.CloseAsync();
            }
            SqliteConnection.ClearAllPools();

            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            string snapshot = Assert.Single(Directory.GetFiles(workingDirectory, "pos-backup-*.db"));

            // The snapshot must be a usable database, not just a file that exists: it is the only
            // copy of the pre-upgrade data the user has left.
            await using var snapshotConnection = new SqliteConnection($"Data Source={snapshot};Mode=ReadOnly");
            await snapshotConnection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = snapshotConnection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Cleanup(workingDirectory);
        }
    }

    [Fact]
    public async Task EnsureLatestSchemaAsync_OnAFreshInstall_DoesNotLeaveASnapshotBehind()
    {
        string workingDirectory = CreateWorkingDirectory();
        string dbPath = Path.Combine(workingDirectory, "pos.db");

        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            Assert.Empty(Directory.GetFiles(workingDirectory, "pos-backup-*.db"));
        }
        finally
        {
            Cleanup(workingDirectory);
        }
    }

    private static string CreateWorkingDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "pos-rollout-snapshot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string workingDirectory)
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
