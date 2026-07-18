using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Preflight;
using Xunit;

namespace POS.Application.Tests.Persistence;

// ponytail: one consolidated file-based smoke test proving the literal "copy a legacy
// database file, then migrate the copy" path from design.md's migration sequence,
// closing the gap that IdentityReceivableMigrationTests/InventoryReturnQuoteMigrationTests
// leave (they migrate a fresh in-memory DB through the full chain rather than a
// pre-existing file). Legacy rows are seeded with raw SQL matching the pre-cutoff schema
// exactly (verified against sqlite_master), because the live AppDbContext model always
// expects the latest columns and cannot write a genuinely older row shape. Not a general
// migration-testing framework: single scenario only.
public sealed class CopiedLegacyDatabaseMigrationSmokeTests
{
    private const string LastPreHardeningMigration = "20260715031926_MakeSaleReturnIndexNonUnique";

    [Fact]
    public async Task CopiedLegacyDatabaseFile_MigratesCleanly_AndPassesPostMigrationPreflight()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "pos-legacy-migration-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        string legacyDbPath = Path.Combine(workingDirectory, "legacy.db");
        string copiedDbPath = Path.Combine(workingDirectory, "legacy-copy.db");

        try
        {
            string roleId = Guid.NewGuid().ToString();
            string cashierId = Guid.NewGuid().ToString();
            string customerId = Guid.NewGuid().ToString();
            string saleId = Guid.NewGuid().ToString();

            // 1. Build a legacy database file: migrate only up to the last pre-audit-hardening
            //    migration, then seed representative pre-change rows with raw SQL matching that
            //    exact schema (no OperationId column, no Receivable.Version/IsCancelled columns).
            await using (var legacyConnection = new SqliteConnection($"Data Source={legacyDbPath}"))
            {
                await legacyConnection.OpenAsync();
                await using (var legacyContext = CreateDbContext(legacyConnection))
                {
                    await legacyContext.GetService<IMigrator>().MigrateAsync(LastPreHardeningMigration);
                }

                await using var seedCommand = legacyConnection.CreateCommand();
                seedCommand.CommandText = $"""
                    INSERT INTO "Roles" ("Id", "Name", "IsSystem", "IsActive", "CreatedAt", "UpdatedAt") VALUES ('{roleId}', 'Admin', 1, 1, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                    INSERT INTO "Users" ("Id", "Username", "PasswordHash", "RoleId", "IsActive", "CreatedAt") VALUES ('{cashierId}', 'legacycashier', 'hash', '{roleId}', 1, '2026-01-01 00:00:00');
                    INSERT INTO "Customers" ("Id", "FullName", "Identification", "IsActive") VALUES ('{customerId}', 'Legacy Customer', '11111111', 1);
                    INSERT INTO "SALES" ("Id", "InvoiceNumber", "CashierUserId", "CustomerId", "Status", "CreatedAt") VALUES ('{saleId}', 'FAC-LEGACY-0001', '{cashierId}', '{customerId}', 2, '2026-01-01 00:00:00');
                    INSERT INTO "SALE_RETURNS" ("Id", "OriginalSaleId", "ProcessedByUserId", "ReturnedAt", "Reason") VALUES ('{Guid.NewGuid()}', '{saleId}', '{cashierId}', '2026-01-02 00:00:00', 'Legacy return, no operation id');
                    INSERT INTO "RECEIVABLES" ("Id", "CustomerId", "SaleId", "original_balance", "outstanding_balance", "CreatedAt") VALUES ('{Guid.NewGuid()}', '{customerId}', '{saleId}', '100', '100', '2026-01-01 00:00:00');
                    """;
                await seedCommand.ExecuteNonQueryAsync();

                await legacyConnection.CloseAsync();
            }
            SqliteConnection.ClearAllPools();

            // 2. Copy the legacy file, exactly as an operator would before running the
            //    hardened rollout against production data.
            File.Copy(legacyDbPath, copiedDbPath);

            // 3. Run the same gate DatabaseSchemaRollout uses: read-only preflight before
            //    MigrateAsync, then apply the remaining (audit-hardening) migrations.
            await using (var copiedConnection = new SqliteConnection($"Data Source={copiedDbPath}"))
            {
                await copiedConnection.OpenAsync();
                await using var copiedContext = CreateDbContext(copiedConnection);

                IntegrityPreflightResult preflightBeforeMigration = await IntegrityPreflight.CheckAsync(copiedContext);
                Assert.True(preflightBeforeMigration.IsValid, string.Join("; ", preflightBeforeMigration.Errors));

                await copiedContext.Database.MigrateAsync();

                // 4. Legacy rows survive unchanged and are still readable post-migration through
                //    the current (post-hardening) entity model.
                Sale? migratedSale = await copiedContext.Sales.FindAsync(Guid.Parse(saleId));
                Assert.NotNull(migratedSale);
                Assert.Equal(Guid.Parse(cashierId), migratedSale!.CashierUserId);

                SaleReturn migratedReturn = await copiedContext.SaleReturns.SingleAsync(r => r.OriginalSaleId == Guid.Parse(saleId));
                Assert.Null(migratedReturn.OperationId);

                Receivable migratedReceivable = await copiedContext.Receivables.SingleAsync(r => r.CustomerId == Guid.Parse(customerId));
                Assert.Equal(100m, migratedReceivable.OutstandingBalance.Amount);

                // 5. The read-only post-migration integrity preflight passes on the migrated copy.
                IntegrityPreflightResult preflightAfterMigration = await IntegrityPreflight.CheckAsync(copiedContext);
                Assert.True(preflightAfterMigration.IsValid, string.Join("; ", preflightAfterMigration.Errors));

                await copiedConnection.CloseAsync();
            }
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            // ponytail: SQLite pooled connections can release their Windows file handle
            // slightly after ClearAllPools returns; best-effort cleanup only, a leftover
            // temp file under %TEMP% is not worth failing an otherwise-passing test over.
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
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
