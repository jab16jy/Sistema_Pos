using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Settings;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public async Task GetAsync_WhenDatabaseHasNoSettings_ReturnsCompanyDefaults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var repository = new SettingsRepository(dbContext);

        BusinessSettings? settings = await repository.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(settings);
        Assert.Equal("ESTHETIC MEDICAL STORE", settings.BusinessName);
        Assert.Equal("cra 42f #76-77 local 1.", settings.BusinessAddress);
        Assert.Equal("301 5225337", settings.BusinessPhone);
        Assert.Equal("COP", settings.CurrencyCode);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
