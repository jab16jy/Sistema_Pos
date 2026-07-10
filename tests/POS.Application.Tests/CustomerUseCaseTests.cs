using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class CustomerUseCaseTests
{
    [Fact]
    public async Task SaveCustomerAsync_WithNewCustomer_PersistsSearchableCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useCase = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);

        CustomerResult result = await useCase.ExecuteAsync(new SaveCustomerRequest(
            null,
            "Ana Gomez",
            "123",
            "3001234567",
            "ana@example.com",
            "Main Street 1",
            true),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Ana Gomez", result.FullName);
        IReadOnlyList<Customer> search = await new CustomerRepository(dbContext)
            .SearchAsync("ana", true, TestContext.Current.CancellationToken);
        Customer match = Assert.Single(search);
        Assert.Equal(result.Id, match.Id);
        Assert.Equal("123", match.Identification);
    }

    [Fact]
    public async Task SaveCustomerAsync_WithMissingName_RejectsWithoutSaving()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useCase = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new SaveCustomerRequest(
            null,
            " ",
            "123",
            null,
            null,
            null,
            true),
            TestContext.Current.CancellationToken));

        Assert.Empty(await dbContext.Customers.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveCustomerAsync_WithDuplicateIdentification_ThrowsAndDoesNotCreateSecond()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useCase = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);
        await useCase.ExecuteAsync(new SaveCustomerRequest(null, "Ana Gomez", "123", null, null, null, true), TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new SaveCustomerRequest(null, "Otro Cliente", "123", null, null, null, true),
            TestContext.Current.CancellationToken));

        Assert.Contains("123", ex.Message);
        Assert.Single(await dbContext.Customers.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveCustomerAsync_EditingKeepsItsOwnIdentification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useCase = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);
        CustomerResult created = await useCase.ExecuteAsync(new SaveCustomerRequest(null, "Ana Gomez", "123", null, null, null, true), TestContext.Current.CancellationToken);

        // Re-saving the same customer with the same identification must not trip the uniqueness guard.
        CustomerResult updated = await useCase.ExecuteAsync(new SaveCustomerRequest(created.Id, "Ana Maria", "123", null, null, null, true), TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Ana Maria", updated.FullName);
    }

    [Fact]
    public async Task SaveCustomerAsync_WithExistingCustomer_UpdatesThenDeactivatesRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useCase = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);
        CustomerResult created = await useCase.ExecuteAsync(new SaveCustomerRequest(null, "Ana Gomez", "123", null, null, null, true), TestContext.Current.CancellationToken);

        CustomerResult updated = await useCase.ExecuteAsync(new SaveCustomerRequest(created.Id, "Ana Maria", "456", "+57 301 222 3344", null, null, false), TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Ana Maria", updated.FullName);
        Assert.False(updated.IsActive);
        Assert.Empty(await new CustomerRepository(dbContext).SearchAsync("Ana", true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchCustomersAsync_CanIncludeInactiveCustomersAndCarriesAddressForEditing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var save = new SaveCustomerUseCase(new CustomerRepository(dbContext), dbContext);
        await save.ExecuteAsync(new SaveCustomerRequest(null, "Ana Gomez", "123", null, null, "Main Street 1", false), TestContext.Current.CancellationToken);
        var search = new SearchCustomersUseCase(new CustomerLookupQueryService(dbContext));

        IReadOnlyList<CustomerSearchResult> activeOnly = await search.ExecuteAsync(new CustomerSearchRequest("Ana", IncludeInactive: false), TestContext.Current.CancellationToken);
        IReadOnlyList<CustomerSearchResult> all = await search.ExecuteAsync(new CustomerSearchRequest("Ana", IncludeInactive: true), TestContext.Current.CancellationToken);

        Assert.Empty(activeOnly);
        CustomerSearchResult inactive = Assert.Single(all);
        Assert.False(inactive.IsActive);
        Assert.Equal("Main Street 1", inactive.Address);
    }

    [Fact]
    public async Task GetCustomerHistoryAsync_WithSalesAndNoHistory_ReturnsPersistedRowsAndZeroState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var customer = new Customer("Ana Gomez", "123");
        var otherCustomer = new Customer("No Sales", "999");
        var sale = new Sale(Guid.NewGuid(), customer.Id);
        var product = new Product("Sugar", Guid.NewGuid(), new Domain.ValueObjects.Money(1_000m), new Domain.ValueObjects.Money(1_400m), new Domain.ValueObjects.StockQuantity(10m), new Domain.ValueObjects.StockQuantity(2m));
        sale.AddItem(product, new Domain.ValueObjects.Quantity(2m), Domain.ValueObjects.Money.Zero);
        sale.AddPayment(new Payment(Domain.Enums.PaymentMethod.Cash, new Domain.ValueObjects.Money(2_800m), new Domain.ValueObjects.Money(3_000m)));
        sale.Complete();
        await dbContext.Customers.AddRangeAsync([customer, otherCustomer], TestContext.Current.CancellationToken);
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new GetCustomerHistoryUseCase(new CustomerHistoryQueryService(dbContext));

        CustomerHistoryResult history = await useCase.ExecuteAsync(customer.Id, TestContext.Current.CancellationToken);
        CustomerHistoryResult empty = await useCase.ExecuteAsync(otherCustomer.Id, TestContext.Current.CancellationToken);

        CustomerHistoryEntry saleEntry = Assert.Single(history.Entries);
        Assert.Equal("Sale", saleEntry.Kind);
        Assert.Equal(sale.Id, saleEntry.SaleId);
        Assert.Equal(2_800m, saleEntry.Amount);
        Assert.Empty(empty.Entries);
    }

    [Fact]
    public async Task GetCustomerHistoryAsync_IncludesCustomerLinkedOperationalSalesBeyondCompletedOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var customer = new Customer("Ana Gomez", "123");
        var draftSale = new Sale(Guid.NewGuid(), customer.Id);
        var product = new Product("Rice", Guid.NewGuid(), new Domain.ValueObjects.Money(1_000m), new Domain.ValueObjects.Money(1_500m), new Domain.ValueObjects.StockQuantity(10m), new Domain.ValueObjects.StockQuantity(2m));
        draftSale.AddItem(product, new Domain.ValueObjects.Quantity(1m), Domain.ValueObjects.Money.Zero);
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.Sales.AddAsync(draftSale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new GetCustomerHistoryUseCase(new CustomerHistoryQueryService(dbContext));

        CustomerHistoryResult history = await useCase.ExecuteAsync(customer.Id, TestContext.Current.CancellationToken);

        CustomerHistoryEntry entry = Assert.Single(history.Entries);
        Assert.Equal("Sale", entry.Kind);
        Assert.Equal(draftSale.Id, entry.SaleId);
        Assert.Equal("Draft", entry.Status);
        Assert.Equal(1_500m, entry.Amount);
    }

    [Fact]
    public async Task GetCustomerHistoryAsync_IncludesLinkedQuotesAndReceivableBalance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var customer = new Customer("Ana Gomez", "123");
        var product = new Product("Rice", Guid.NewGuid(), new Domain.ValueObjects.Money(1_000m), new Domain.ValueObjects.Money(1_500m), new Domain.ValueObjects.StockQuantity(10m), new Domain.ValueObjects.StockQuantity(2m));
        var sale = new Sale(Guid.NewGuid(), customer.Id);
        sale.AddItem(product, new Domain.ValueObjects.Quantity(1m), Domain.ValueObjects.Money.Zero);
        var quote = new Quote("COT-2026-0001", "Ana Gomez", 30, customer.Id, "+57 300 123 4567", "ana@example.com");
        quote.AddItem(new QuoteItem(product.Id, product.Name, new Domain.ValueObjects.Money(1_500m), new Domain.ValueObjects.Quantity(2m), 0m));
        var receivable = new Receivable(customer.Id, sale.Id, new Domain.ValueObjects.Money(5_000m));
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.Quotes.AddAsync(quote, TestContext.Current.CancellationToken);
        await dbContext.Receivables.AddAsync(receivable, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new GetCustomerHistoryUseCase(new CustomerHistoryQueryService(dbContext));

        CustomerHistoryResult history = await useCase.ExecuteAsync(customer.Id, TestContext.Current.CancellationToken);

        Assert.Contains(history.Entries, entry => entry.Kind == "Quote" && entry.Reference == "COT-2026-0001" && entry.Amount == 3_000m);
        Assert.Contains(history.Entries, entry => entry.Kind == "Receivable" && entry.Amount == 5_000m && entry.Status == "Pending");
        Assert.Equal(5_000m, history.OutstandingBalance);
    }

    [Fact]
    public async Task DatabaseSchemaRolloutAsync_CreatesCustomersTableForExistingLocalDatabase()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-schema-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(dbPath, [], TestContext.Current.CancellationToken);
        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Customers'";
            long tableCount = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
            Assert.Equal(1L, tableCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void AddPosInfrastructure_RegistersCustomerManagementContracts()
    {
        var services = new ServiceCollection();

        services.AddPosInfrastructure("Data Source=:memory:");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SaveCustomerUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetCustomerHistoryUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchCustomersUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICustomerRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICustomerLookupQueryService>());
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
