using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests;

public sealed class RuntimeReadinessTests
{
    [Fact]
    public void AddPosInfrastructure_RegistersRuntimeReadinessContracts()
    {
        var services = new ServiceCollection();

        services.AddPosInfrastructure("Data Source=:memory:");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWhatsAppService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPrintService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductLookupService>());
    }

    [Fact]
    public async Task SmtpEmailService_WhenConfigurationIsMissing_FailsWithRecoverableMessage()
    {
        var service = new SmtpEmailService(new SmtpRuntimeOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(
                "client@example.com",
                "Subject",
                "Body",
                [1, 2, 3],
                "document.pdf",
                TestContext.Current.CancellationToken));

        Assert.Contains("SMTP", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppScriptService_WhenScriptIsMissing_FailsWithRecoverableMessage()
    {
        var service = new WhatsAppScriptService(new WhatsAppScriptRuntimeOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(
                "573001234567",
                "Documento",
                null,
                TestContext.Current.CancellationToken));

        Assert.Contains("WhatsApp", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductLookupService_SearchAsync_ReturnsActivePersistedProductByNameOrBarcode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var active = new Product(
            "Vitamin C",
            Guid.NewGuid(),
            new Money(8_000m),
            new Money(12_000m),
            new StockQuantity(7m),
            new StockQuantity(2m),
            barcode: new Barcode("VITC-001"));
        var inactive = new Product(
            "Vitamin D",
            Guid.NewGuid(),
            new Money(8_000m),
            new Money(12_000m),
            new StockQuantity(7m),
            new StockQuantity(2m),
            barcode: new Barcode("VITD-001"));
        inactive.Deactivate();

        await dbContext.Products.AddRangeAsync([active, inactive], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProductLookupService(dbContext);

        var nameResults = await service.SearchAsync("Vitamin", TestContext.Current.CancellationToken);
        var barcodeResults = await service.SearchAsync("VITC-001", TestContext.Current.CancellationToken);

        var result = Assert.Single(nameResults);
        Assert.Equal(active.Id, result.ProductId);
        Assert.Equal("Vitamin C", result.Name);
        Assert.Equal("VITC-001", result.Barcode);
        Assert.Equal(12_000m, result.UnitPrice);
        Assert.Equal(7m, result.AvailableStock);
        Assert.Equal(active.Id, Assert.Single(barcodeResults).ProductId);
    }

    [Fact]
    public async Task ReportService_GenerateDailySalesReportAsync_ReturnsNonEmptyPdfBytes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var product = new Product(
            "Serum",
            Guid.NewGuid(),
            new Money(20_000m),
            new Money(35_000m),
            new StockQuantity(5m),
            new StockQuantity(1m));
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext);
        await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                Guid.NewGuid(),
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 35_000m, 40_000m, null)]),
            TestContext.Current.CancellationToken);

        var service = new ReportService(dbContext);

        byte[] bytes = await service.GenerateDailySalesReportAsync(
            DateOnly.FromDateTime(DateTime.UtcNow),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public async Task PrintService_WhenPrinterIsMissing_FailsWithRecoverableMessage()
    {
        var service = new PrintService(new PrinterRuntimeOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrintReceiptAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Contains("impresora", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not implemented", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
