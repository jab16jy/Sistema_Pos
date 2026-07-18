using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Preflight;
using Xunit;

namespace POS.Application.Tests.Persistence;

public sealed class IntegrityPreflightTests
{
    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection)
    {
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
    }

    private static async Task ExecuteRawSqlAsync(SqliteConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CheckAsync_WhenDatabaseIsEmpty_ReturnsValid()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task CheckAsync_WithDuplicateUsernames_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string userId1 = Guid.NewGuid().ToString();
        string userId2 = Guid.NewGuid().ToString();
        string roleId = Guid.NewGuid().ToString();

        await ExecuteRawSqlAsync(connection, "DROP INDEX IF EXISTS \"IX_Users_Username\";");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsSystem\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ('{roleId}', 'Role1', 1, 1, '2026-01-01', '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId1}', 'dupuser', 'hash', '{roleId}', 1, '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId2}', 'dupuser', 'hash2', '{roleId}', 1, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate username 'dupuser'"));
    }

    [Fact]
    public async Task CheckAsync_WithCaseDistinctUsernames_PassesPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string userId1 = Guid.NewGuid().ToString();
        string userId2 = Guid.NewGuid().ToString();
        string roleId = Guid.NewGuid().ToString();

        await ExecuteRawSqlAsync(connection, "DROP INDEX IF EXISTS \"IX_Users_Username\";");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsSystem\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ('{roleId}', 'Role1', 1, 1, '2026-01-01', '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId1}', 'distinctuser', 'hash', '{roleId}', 1, '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId2}', 'DistinctUser', 'hash2', '{roleId}', 1, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task CheckAsync_WithOrphanSaleCashierUser_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string saleId = Guid.NewGuid().ToString();
        string missingUserId = Guid.NewGuid().ToString();
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"SALES\" (\"Id\", \"CashierUserId\", \"InvoiceNumber\", \"Status\", \"CreatedAt\") VALUES ('{saleId}', '{missingUserId}', 'INV-001', 1, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SALES") && e.Contains("CashierUserId -> Users.Id"));
    }

    [Fact]
    public async Task CheckAsync_WithOrphanSaleReturnProcessor_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string returnId = Guid.NewGuid().ToString();
        string missingUserId = Guid.NewGuid().ToString();
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"SALE_RETURNS\" (\"Id\", \"OriginalSaleId\", \"ProcessedByUserId\", \"Reason\", \"ReturnedAt\") VALUES ('{returnId}', '{Guid.NewGuid()}', '{missingUserId}', 'Reason', '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SALE_RETURNS") && e.Contains("ProcessedByUserId -> Users.Id"));
    }

    [Fact]
    public async Task CheckAsync_WithOrphanShiftCashierUser_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string shiftId = Guid.NewGuid().ToString();
        string missingUserId = Guid.NewGuid().ToString();
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"CASHIER_SHIFTS\" (\"Id\", \"CashierUserId\", \"CashierUsername\", \"opening_cash\", \"closing_cash\", \"Status\", \"OpenedAt\") VALUES ('{shiftId}', '{missingUserId}', 'unknown', 0, 0, 0, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CASHIER_SHIFTS") && e.Contains("CashierUserId -> Users.Id"));
    }

    [Fact]
    public async Task CheckAsync_WithOrphanReceivableCustomer_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string recId = Guid.NewGuid().ToString();
        string missingCustomerId = Guid.NewGuid().ToString();
        string saleId = Guid.NewGuid().ToString();
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"RECEIVABLES\" (\"Id\", \"CustomerId\", \"SaleId\", \"original_balance\", \"outstanding_balance\", \"CreatedAt\") VALUES ('{recId}', '{missingCustomerId}', '{saleId}', 100, 100, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("RECEIVABLES") && e.Contains("CustomerId -> Customers.Id"));
    }

    [Fact]
    public async Task CheckAsync_WithOrphanReceivableSale_FailsPreflight()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string recId = Guid.NewGuid().ToString();
        string customerId = Guid.NewGuid().ToString();
        string missingSaleId = Guid.NewGuid().ToString();

        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Customers\" (\"Id\", \"FullName\", \"Identification\", \"IsActive\") VALUES ('{customerId}', 'Customer 1', 'ID1', 1);");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"RECEIVABLES\" (\"Id\", \"CustomerId\", \"SaleId\", \"original_balance\", \"outstanding_balance\", \"CreatedAt\") VALUES ('{recId}', '{customerId}', '{missingSaleId}', 100, 100, '2026-01-01');");

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("RECEIVABLES") && e.Contains("SaleId -> Sales.Id"));
    }

    [Fact]
    public async Task VerifyAsync_WhenFailed_ThrowsInvalidOperationException()
    {
        await using var connection = await OpenConnectionAsync();
        await CreateSchemaAsync(connection);
        await using var dbContext = CreateDbContext(connection);

        string userId1 = Guid.NewGuid().ToString();
        string userId2 = Guid.NewGuid().ToString();
        string roleId = Guid.NewGuid().ToString();

        await ExecuteRawSqlAsync(connection, "DROP INDEX IF EXISTS \"IX_Users_Username\";");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsSystem\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ('{roleId}', 'Role1', 1, 1, '2026-01-01', '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId1}', 'baduser', 'hash', '{roleId}', 1, '2026-01-01');");
        await ExecuteRawSqlAsync(connection, $"INSERT INTO \"Users\" (\"Id\", \"Username\", \"PasswordHash\", \"RoleId\", \"IsActive\", \"CreatedAt\") VALUES ('{userId2}', 'baduser', 'hash2', '{roleId}', 1, '2026-01-01');");

        await Assert.ThrowsAsync<InvalidOperationException>(() => IntegrityPreflight.VerifyAsync(dbContext));
    }
}
