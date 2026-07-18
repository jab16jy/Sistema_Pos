using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Persistence.Preflight;

public record IntegrityPreflightResult(bool IsValid, IReadOnlyList<string> Errors);

public static class IntegrityPreflight
{
    public static async Task<IntegrityPreflightResult> CheckAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        bool hasUsers = await TableExistsAsync(dbContext, "Users", cancellationToken).ConfigureAwait(false);
        bool hasSales = await TableExistsAsync(dbContext, "SALES", cancellationToken).ConfigureAwait(false);
        bool hasReturns = await TableExistsAsync(dbContext, "SALE_RETURNS", cancellationToken).ConfigureAwait(false);
        bool hasShifts = await TableExistsAsync(dbContext, "CASHIER_SHIFTS", cancellationToken).ConfigureAwait(false);
        bool hasCustomers = await TableExistsAsync(dbContext, "Customers", cancellationToken).ConfigureAwait(false);
        bool hasReceivables = await TableExistsAsync(dbContext, "RECEIVABLES", cancellationToken).ConfigureAwait(false);
        bool hasProducts = await TableExistsAsync(dbContext, "PRODUCTS", cancellationToken).ConfigureAwait(false);
        bool hasInventoryMovements = await TableExistsAsync(dbContext, "InventoryMovements", cancellationToken).ConfigureAwait(false);
        bool hasReturnItems = await TableExistsAsync(dbContext, "SALE_RETURN_ITEMS", cancellationToken).ConfigureAwait(false);
        bool hasSaleItems = await TableExistsAsync(dbContext, "SALE_ITEMS", cancellationToken).ConfigureAwait(false);
        bool hasQuotes = await TableExistsAsync(dbContext, "QUOTES", cancellationToken).ConfigureAwait(false);

        var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasUsers)
        {
            var users = await QueryTwoColumnsAsync(dbContext, "Users", "Id", "Username", cancellationToken).ConfigureAwait(false);
            foreach (var (id, username) in users)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    userIds.Add(id);
                }
            }

            var duplicateUsernames = users
                .Where(u => !string.IsNullOrEmpty(u.Val2))
                .GroupBy(u => u.Val2!, StringComparer.Ordinal)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateUsernames)
            {
                var ids = string.Join(", ", group.Select(x => x.Val1));
                errors.Add($"Duplicate username '{group.Key}' found for User IDs: {ids}");
            }
        }

        if (hasUsers && hasSales)
        {
            var sales = await QueryTwoColumnsAsync(dbContext, "SALES", "Id", "CashierUserId", cancellationToken).ConfigureAwait(false);
            foreach (var (saleId, cashierUserId) in sales)
            {
                if (!string.IsNullOrEmpty(cashierUserId) && !userIds.Contains(cashierUserId))
                {
                    errors.Add($"Table: SALES, RecordId: {saleId}, Relationship: CashierUserId -> Users.Id, Missing Principal: {cashierUserId}");
                }
            }
        }

        if (hasUsers && hasReturns)
        {
            var returns = await QueryTwoColumnsAsync(dbContext, "SALE_RETURNS", "Id", "ProcessedByUserId", cancellationToken).ConfigureAwait(false);
            foreach (var (returnId, processedByUserId) in returns)
            {
                if (!string.IsNullOrEmpty(processedByUserId) && !userIds.Contains(processedByUserId))
                {
                    errors.Add($"Table: SALE_RETURNS, RecordId: {returnId}, Relationship: ProcessedByUserId -> Users.Id, Missing Principal: {processedByUserId}");
                }
            }
        }

        if (hasUsers && hasShifts)
        {
            var shifts = await QueryTwoColumnsAsync(dbContext, "CASHIER_SHIFTS", "Id", "CashierUserId", cancellationToken).ConfigureAwait(false);
            foreach (var (shiftId, cashierUserId) in shifts)
            {
                if (!string.IsNullOrEmpty(cashierUserId) && !userIds.Contains(cashierUserId))
                {
                    errors.Add($"Table: CASHIER_SHIFTS, RecordId: {shiftId}, Relationship: CashierUserId -> Users.Id, Missing Principal: {cashierUserId}");
                }
            }
        }

        var customerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasCustomers)
        {
            var customers = await QueryTwoColumnsAsync(dbContext, "Customers", "Id", "FullName", cancellationToken).ConfigureAwait(false);
            foreach (var (id, _) in customers)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    customerIds.Add(id);
                }
            }
        }

        var saleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasSales)
        {
            var sales = await QueryTwoColumnsAsync(dbContext, "SALES", "Id", "CashierUserId", cancellationToken).ConfigureAwait(false);
            foreach (var (id, _) in sales)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    saleIds.Add(id);
                }
            }
        }

        if (hasReceivables)
        {
            var receivables = await QueryThreeColumnsAsync(dbContext, "RECEIVABLES", "Id", "CustomerId", "SaleId", cancellationToken).ConfigureAwait(false);
            foreach (var (recId, customerId, saleId) in receivables)
            {
                if (hasCustomers && !string.IsNullOrEmpty(customerId) && !customerIds.Contains(customerId))
                {
                    errors.Add($"Table: RECEIVABLES, RecordId: {recId}, Relationship: CustomerId -> Customers.Id, Missing Principal: {customerId}");
                }

                if (hasSales && !string.IsNullOrEmpty(saleId) && !saleIds.Contains(saleId))
                {
                    errors.Add($"Table: RECEIVABLES, RecordId: {recId}, Relationship: SaleId -> Sales.Id, Missing Principal: {saleId}");
                }
            }
        }

        var productIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasProducts)
        {
            var products = await QueryTwoColumnsAsync(dbContext, "PRODUCTS", "Id", "Id", cancellationToken).ConfigureAwait(false);
            foreach (var (id, _) in products)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    productIds.Add(id);
                }
            }
        }

        if (hasProducts && hasInventoryMovements)
        {
            var movements = await QueryTwoColumnsAsync(dbContext, "InventoryMovements", "Id", "ProductId", cancellationToken).ConfigureAwait(false);
            foreach (var (movementId, productId) in movements)
            {
                if (!string.IsNullOrEmpty(productId) && !productIds.Contains(productId))
                {
                    errors.Add($"Table: InventoryMovements, RecordId: {movementId}, Relationship: ProductId -> PRODUCTS.Id, Missing Principal: {productId}");
                }
            }
        }

        var saleItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasSaleItems)
        {
            var saleItems = await QueryTwoColumnsAsync(dbContext, "SALE_ITEMS", "Id", "Id", cancellationToken).ConfigureAwait(false);
            foreach (var (id, _) in saleItems)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    saleItemIds.Add(id);
                }
            }
        }

        if (hasReturnItems)
        {
            var returnItems = await QueryFourColumnsAsync(dbContext, "SALE_RETURN_ITEMS", "Id", "SaleReturnId", "SaleItemId", "ProductId", cancellationToken).ConfigureAwait(false);
            foreach (var (itemId, _, saleItemId, productId) in returnItems)
            {
                if (hasSaleItems && !string.IsNullOrEmpty(saleItemId) && !saleItemIds.Contains(saleItemId))
                {
                    errors.Add($"Table: SALE_RETURN_ITEMS, RecordId: {itemId}, Relationship: SaleItemId -> SALE_ITEMS.Id, Missing Principal: {saleItemId}");
                }

                if (hasProducts && !string.IsNullOrEmpty(productId) && !productIds.Contains(productId))
                {
                    errors.Add($"Table: SALE_RETURN_ITEMS, RecordId: {itemId}, Relationship: ProductId -> PRODUCTS.Id, Missing Principal: {productId}");
                }
            }
        }

        if (hasQuotes)
        {
            var quotes = await QueryThreeColumnsAsync(dbContext, "QUOTES", "Id", "CustomerId", "ConvertedSaleId", cancellationToken).ConfigureAwait(false);
            foreach (var (quoteId, customerId, convertedSaleId) in quotes)
            {
                if (hasCustomers && !string.IsNullOrEmpty(customerId) && !customerIds.Contains(customerId))
                {
                    errors.Add($"Table: QUOTES, RecordId: {quoteId}, Relationship: CustomerId -> Customers.Id, Missing Principal: {customerId}");
                }

                if (hasSales && !string.IsNullOrEmpty(convertedSaleId) && !saleIds.Contains(convertedSaleId))
                {
                    errors.Add($"Table: QUOTES, RecordId: {quoteId}, Relationship: ConvertedSaleId -> Sales.Id, Missing Principal: {convertedSaleId}");
                }
            }
        }

        return new IntegrityPreflightResult(errors.Count == 0, errors);
    }

    public static async Task VerifyAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var result = await CheckAsync(dbContext, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new InvalidOperationException($"Preflight integrity check failed:\n{string.Join("\n", result.Errors)}");
        }
    }

    private static async Task<bool> TableExistsAsync(DbContext dbContext, string tableName, CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
            var param = command.CreateParameter();
            param.ParameterName = "@name";
            param.Value = tableName;
            command.Parameters.Add(param);

            var count = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
            return count > 0;
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<(string Val1, string? Val2)>> QueryTwoColumnsAsync(DbContext dbContext, string tableName, string col1, string col2, CancellationToken ct)
    {
        var list = new List<(string Val1, string? Val2)>();
        var connection = dbContext.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT \"{col1}\", \"{col2}\" FROM \"{tableName}\";";
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                string val1 = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString()!;
                string? val2 = reader.IsDBNull(1) ? null : reader.GetValue(1).ToString();
                list.Add((val1, val2));
            }
            return list;
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<(string Val1, string? Val2, string? Val3, string? Val4)>> QueryFourColumnsAsync(DbContext dbContext, string tableName, string col1, string col2, string col3, string col4, CancellationToken ct)
    {
        var list = new List<(string Val1, string? Val2, string? Val3, string? Val4)>();
        var connection = dbContext.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT \"{col1}\", \"{col2}\", \"{col3}\", \"{col4}\" FROM \"{tableName}\";";
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                string val1 = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString()!;
                string? val2 = reader.IsDBNull(1) ? null : reader.GetValue(1).ToString();
                string? val3 = reader.IsDBNull(2) ? null : reader.GetValue(2).ToString();
                string? val4 = reader.IsDBNull(3) ? null : reader.GetValue(3).ToString();
                list.Add((val1, val2, val3, val4));
            }
            return list;
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<(string Val1, string? Val2, string? Val3)>> QueryThreeColumnsAsync(DbContext dbContext, string tableName, string col1, string col2, string col3, CancellationToken ct)
    {
        var list = new List<(string Val1, string? Val2, string? Val3)>();
        var connection = dbContext.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT \"{col1}\", \"{col2}\", \"{col3}\" FROM \"{tableName}\";";
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                string val1 = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString()!;
                string? val2 = reader.IsDBNull(1) ? null : reader.GetValue(1).ToString();
                string? val3 = reader.IsDBNull(2) ? null : reader.GetValue(2).ToString();
                list.Add((val1, val2, val3));
            }
            return list;
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
