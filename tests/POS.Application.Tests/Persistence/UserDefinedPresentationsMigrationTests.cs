using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Persistence;

public sealed class UserDefinedPresentationsMigrationTests
{
    [Fact]
    public async Task Migrate_FixedBoxColumnsToUserDefinedPresentations_BackfillsDataAndLineSnapshots()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos_presentation_migration_{Guid.NewGuid():N}.db");
        string connectionString = $"Data Source={databasePath}";

        string productId = Guid.NewGuid().ToString();
        string saleId = Guid.NewGuid().ToString();
        string saleItemId = Guid.NewGuid().ToString();

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync(
                    targetMigration: "20260804003649_AddProductFractionalBoxPresentations",
                    cancellationToken: TestContext.Current.CancellationToken);

                string categoryId = Guid.NewGuid().ToString();
                await db.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO CATEGORIES (Id, Name, IsActive) VALUES ('{categoryId}', 'General', 1);
                ");

                await db.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO PRODUCTS (Id, Name, Description, SourceCode, Barcode, CategoryId, cost_price, sale_price, CurrentStock, MinimumStock, tax_rate, IsActive, version, units_per_box, box_price, half_box_units, half_box_price)
                    VALUES ('{productId}', 'Ampolla 350', 'Ampolla 350', 'AMP350', 'AMP350', '{categoryId}', 6000, 10000, 24, 2, 19, 1, 0, 12, 110000, 6, 57000);
                ");

                string userId = Guid.NewGuid().ToString();
                await db.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO Users (Id, Username, PasswordHash, RoleId, IsActive, CreatedAt)
                    SELECT '{userId}', 'cashier', 'hash', Id, 1, '2026-08-04T00:00:00Z' FROM Roles LIMIT 1;
                ");

                await db.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO SALES (Id, CashierUserId, Status, CreatedAt, InvoiceNumber)
                    VALUES ('{saleId}', '{userId}', 2, '2026-08-04T00:00:00Z', 'FAC-001');
                ");

                await db.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO SALE_ITEMS (Id, SaleId, ProductId, ProductName, Quantity, unit_price, discount, UnitCost, tax_rate, presentation, units_per_presentation)
                    VALUES ('{saleItemId}', '{saleId}', '{productId}', 'Ampolla 350', 2, 57000, 0, 36000, 19, 3, 6);
                ");

                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            using (var db = new AppDbContext(options))
            {
                var presentations = await db.Set<Domain.Entities.ProductPresentation>()
                    .AsNoTracking()
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, presentations.Count);
                Assert.Equal("Caja", presentations[0].Name);
                Assert.Equal(12, presentations[0].Units);
                Assert.Equal(110000m, presentations[0].Price.Amount);

                Assert.Equal("Media caja", presentations[1].Name);
                Assert.Equal(6, presentations[1].Units);
                Assert.Equal(57000m, presentations[1].Price.Amount);

                var saleItem = await db.Set<Domain.Entities.SaleItem>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(si => si.Id == Guid.Parse(saleItemId), TestContext.Current.CancellationToken);

                // Note: EF Core reads DB row directly
                using var conn = new SqliteConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT presentation_name, units_per_presentation FROM SALE_ITEMS LIMIT 1;";
                using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Media caja", reader.GetString(0));
                Assert.Equal(6, reader.GetInt32(1));
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(databasePath);
            }
        }
    }
}
