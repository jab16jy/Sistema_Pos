using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class CreateSaleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSaleIsValid_PersistsSaleAndReturnsTotals()
    {
        var product = new Product("Sugar", Guid.NewGuid(), new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new CreateSaleUseCase(products, sales, unitOfWork);

        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.SaleId);
        Assert.Equal(2_800m, result.Total);
        Assert.Equal(2_800m, result.CashInDrawer);
        Assert.Single(sales.Sales);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product _product;

        public InMemoryProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_product.Barcode == barcode ? _product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
