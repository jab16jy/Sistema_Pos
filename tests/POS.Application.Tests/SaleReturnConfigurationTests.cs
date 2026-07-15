using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public sealed class SaleReturnConfigurationTests
{
    [Fact]
    public void SaleReturnConfiguration_OriginalSaleIdIndex_IsNonUnique()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new AppDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(SaleReturn));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(SaleReturn.OriginalSaleId)));

        Assert.NotNull(index);
        Assert.False(index.IsUnique, "The index on OriginalSaleId must be configured as non-unique (IsUnique = false).");
    }
}
