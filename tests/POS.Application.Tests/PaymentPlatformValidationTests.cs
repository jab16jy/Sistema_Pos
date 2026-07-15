using POS.Application.Common;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class PaymentPlatformValidationTests
{
    // --- PaymentPlatforms static class ---

    [Theory]
    [InlineData("Nequi", true)]
    [InlineData("Bancolombia", true)]
    [InlineData("Daviplata", true)]
    [InlineData("Efectivo", true)]
    [InlineData("nequi", true)]           // case-insensitive
    [InlineData("BANCOLOMBIA", true)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsCorrectResult(string? platform, bool expected)
    {
        Assert.Equal(expected, PaymentPlatforms.IsValid(platform));
    }

    [Fact]
    public void All_ContainsFourPlatforms()
    {
        Assert.Equal(4, PaymentPlatforms.All.Count);
    }

    // --- CreateSaleUseCase platform validation ---

    [Fact]
    public async Task CreateSale_WithUnknownPlatform_ThrowsBeforeSavingAnything()
    {
        var product = MakeProduct();
        var sales = new InMemSaleRepo();
        var useCase = new CreateSaleUseCase(
            new InMemProductRepo(product), sales, new InMemUoW());

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_500m, 1_500m, "Zelle")]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales); // sale was not saved
    }

    [Fact]
    public async Task CreateSale_WithNullPlatform_DoesNotThrow()
    {
        var product = MakeProduct();
        var useCase = new CreateSaleUseCase(
            new InMemProductRepo(product), new InMemSaleRepo(), new InMemUoW());

        // null BankOrPlatform is allowed (Cash or non-platform payment)
        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_500m, 1_500m, null)]);

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.SaleId);
    }

    [Fact]
    public async Task CreateSale_WithValidPlatform_Succeeds()
    {
        var product = MakeProduct();
        var useCase = new CreateSaleUseCase(
            new InMemProductRepo(product), new InMemSaleRepo(), new InMemUoW());

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_500m, null, "Nequi")]);

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.SaleId);
    }

    [Fact]
    public async Task CreateSale_TransferWithoutPlatform_ThrowsBeforeSavingAnything()
    {
        var product = MakeProduct();
        var sales = new InMemSaleRepo();
        var useCase = new CreateSaleUseCase(
            new InMemProductRepo(product), sales, new InMemUoW());

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_500m, null, null)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales);
    }

    [Fact]
    public async Task CreateSale_NonTransferWithPlatform_ThrowsBeforeSavingAnything()
    {
        var product = MakeProduct();
        var sales = new InMemSaleRepo();
        var useCase = new CreateSaleUseCase(
            new InMemProductRepo(product), sales, new InMemUoW());

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_500m, 1_500m, "Nequi")]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales);
    }

    // --- Helpers ---

    private static Product MakeProduct() =>
        new("Widget", Guid.NewGuid(), new Money(1_000m), new Money(1_500m), new StockQuantity(10m), new StockQuantity(2m));

    private sealed class InMemProductRepo : IProductRepository
    {
        private readonly Product _product;
        public InMemProductRepo(Product product) => _product = product;
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _product.Id ? _product : null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
            => Task.FromResult<Product?>(null);
        public Task AddAsync(Product product, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemSaleRepo : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];
        public Task AddAsync(Sale sale, CancellationToken ct = default) { Sales.Add(sale); return Task.CompletedTask; }
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Sale?>(null);
        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InMemUoW : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
