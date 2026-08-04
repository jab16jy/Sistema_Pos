using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Application.Tests;

/// <summary>
/// Inserts a PRODUCTS row with raw SQL, naming only the columns that already existed when the
/// migrations under test shipped.
///
/// Migration tests rewind the schema to a historical point and then seed data. Seeding through
/// the EF model instead makes every one of them fail the moment the current model gains a new
/// PRODUCTS column, because EF names that column in the INSERT while the rewound schema has yet
/// to create it. That failure says nothing about the migration being tested.
/// </summary>
internal static class LegacyProductRow
{
    public static Task InsertAsync(AppDbContext dbContext, Product product) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "PRODUCTS"
                ("Id", "SourceCode", "Barcode", "Name", "Description", "CategoryId",
                 "cost_price", "sale_price", "CurrentStock", "MinimumStock", "tax_rate",
                 "ItemType", "TracksInventory", "IsActive")
            VALUES ($id, $sourceCode, $barcode, $name, $description, $categoryId,
                    $costPrice, $salePrice, $currentStock, $minimumStock, $taxRate,
                    $itemType, $tracksInventory, $isActive)
            """,
            new SqliteParameter("$id", product.Id.ToString()),
            new SqliteParameter("$sourceCode", product.SourceCode),
            new SqliteParameter("$barcode", (object?)product.Barcode ?? DBNull.Value),
            new SqliteParameter("$name", product.Name),
            new SqliteParameter("$description", product.Description),
            new SqliteParameter("$categoryId", product.CategoryId.ToString()),
            new SqliteParameter("$costPrice", product.CostPrice.HasValue ? Text(product.CostPrice.Value.Amount) : DBNull.Value),
            new SqliteParameter("$salePrice", Text(product.SalePrice.Amount)),
            new SqliteParameter("$currentStock", Text(product.CurrentStock)),
            new SqliteParameter("$minimumStock", Text(product.MinimumStock)),
            new SqliteParameter("$taxRate", Text(product.TaxRate)),
            new SqliteParameter("$itemType", product.ItemType.ToString()),
            new SqliteParameter("$tracksInventory", product.TracksInventory ? 1 : 0),
            new SqliteParameter("$isActive", product.IsActive ? 1 : 0));

    // Money and stock columns are TEXT; write them the way EF reads them back.
    private static object Text(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
