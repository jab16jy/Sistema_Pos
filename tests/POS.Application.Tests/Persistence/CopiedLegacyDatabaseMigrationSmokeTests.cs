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

            // 3. Apply the remaining (audit-hardening) migrations in the order DatabaseSchemaRollout
            //    uses: migrate first, then run the read-only integrity preflight over the result.
            await using (var copiedConnection = new SqliteConnection($"Data Source={copiedDbPath}"))
            {
                await copiedConnection.OpenAsync();
                await using var copiedContext = CreateDbContext(copiedConnection);

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

    // The upgrade path that actually broke in the field: a legacy database whose sales, shifts and
    // returns point at a user that no longer exists, plus a return registered before SALE_RETURN_ITEMS
    // had a SaleItemId column. Both were fatal — the integrity preflight aborted startup before the
    // migrations that repair the data could run, and the repair itself deleted the history.
    [Fact]
    public async Task DirtyLegacyDatabase_KeepsItsHistory_AndPassesPostMigrationPreflight()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "pos-dirty-legacy-migration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        string legacyDbPath = Path.Combine(workingDirectory, "dirty-legacy.db");

        try
        {
            string ghostUserId = Guid.NewGuid().ToString();
            string categoryId = Guid.NewGuid().ToString();
            string productId = Guid.NewGuid().ToString();
            string saleId = Guid.NewGuid().ToString();
            string saleItemId = Guid.NewGuid().ToString();
            string saleReturnId = Guid.NewGuid().ToString();
            string shiftId = Guid.NewGuid().ToString();

            await using (var legacyConnection = new SqliteConnection($"Data Source={legacyDbPath}"))
            {
                await legacyConnection.OpenAsync();
                await using (var legacyContext = CreateDbContext(legacyConnection))
                {
                    await legacyContext.GetService<IMigrator>().MigrateAsync(LastPreHardeningMigration);
                }

                // No Users row for ghostUserId: the cashier was removed from the old database while
                // the financial records they produced stayed behind. SALE_RETURN_ITEMS is seeded
                // without SaleItemId because that column does not exist yet at this migration.
                await using var seedCommand = legacyConnection.CreateCommand();
                seedCommand.CommandText = $"""
                    INSERT INTO "CATEGORIES" ("Id", "Name", "IsActive") VALUES ('{categoryId}', 'Legacy Category', 1);
                    INSERT INTO "PRODUCTS" ("Id", "Name", "Description", "CategoryId", "cost_price", "sale_price", "CurrentStock", "MinimumStock", "tax_rate", "IsActive", "ItemType", "SourceCode", "TracksInventory")
                        VALUES ('{productId}', 'Legacy Product', 'Seeded', '{categoryId}', '50', '100', '10', '1', '19', 1, 'Product', '', 1);
                    INSERT INTO "SALES" ("Id", "InvoiceNumber", "CashierUserId", "Status", "CreatedAt")
                        VALUES ('{saleId}', 'FAC-DIRTY-0001', '{ghostUserId}', 2, '2026-01-01 00:00:00');
                    INSERT INTO "SALE_ITEMS" ("Id", "SaleId", "ProductId", "ProductName", "Quantity", "unit_price", "discount", "UnitCost")
                        VALUES ('{saleItemId}', '{saleId}', '{productId}', 'Legacy Product', '3', '100', '0', '50');
                    INSERT INTO "CASHIER_SHIFTS" ("Id", "CashierUserId", "CashierUsername", "opening_cash", "closing_cash", "Status", "OpenedAt")
                        VALUES ('{shiftId}', '{ghostUserId}', 'ghost', '100', '0', 0, '2026-01-01 00:00:00');
                    INSERT INTO "SALE_RETURNS" ("Id", "OriginalSaleId", "ProcessedByUserId", "ReturnedAt", "Reason")
                        VALUES ('{saleReturnId}', '{saleId}', '{ghostUserId}', '2026-01-02 00:00:00', 'Partial return, legacy schema');
                    INSERT INTO "SALE_RETURN_ITEMS" ("Id", "SaleReturnId", "ProductId", "ProductName", "QuantityReturned", "UnitPrice", "RefundAmount")
                        VALUES ('{Guid.NewGuid()}', '{saleReturnId}', '{productId}', 'Legacy Product', 1, '100', '100');
                    """;
                await seedCommand.ExecuteNonQueryAsync();

                await legacyConnection.CloseAsync();
            }
            SqliteConnection.ClearAllPools();

            await using (var upgradedConnection = new SqliteConnection($"Data Source={legacyDbPath}"))
            {
                await upgradedConnection.OpenAsync();
                await using var upgradedContext = CreateDbContext(upgradedConnection);

                await upgradedContext.Database.MigrateAsync();

                IntegrityPreflightResult preflight = await IntegrityPreflight.CheckAsync(upgradedContext);
                Assert.True(preflight.IsValid, string.Join("; ", preflight.Errors));

                // The removed cashier comes back as a locked-out placeholder, so nothing they touched
                // had to be deleted to satisfy the new foreign keys.
                User placeholder = await upgradedContext.Users.SingleAsync(user => user.Id == Guid.Parse(ghostUserId));
                Assert.False(placeholder.IsActive);

                Assert.NotNull(await upgradedContext.Sales.FindAsync(Guid.Parse(saleId)));
                Assert.NotNull(await upgradedContext.CashierShifts.FindAsync(Guid.Parse(shiftId)));

                // The return keeps its line, and the line is reattached to the sale item it came from.
                // An empty Items collection would silently mean "the whole sale was returned".
                SaleReturn migratedReturn = await upgradedContext.SaleReturns
                    .Include(saleReturn => saleReturn.Items)
                    .SingleAsync(saleReturn => saleReturn.Id == Guid.Parse(saleReturnId));
                SaleReturnItem returnedLine = Assert.Single(migratedReturn.Items);
                Assert.Equal(Guid.Parse(saleItemId), returnedLine.SaleItemId);
                Assert.Equal(1, returnedLine.QuantityReturned);

                await upgradedConnection.CloseAsync();
            }
            SqliteConnection.ClearAllPools();
        }
        finally
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
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
