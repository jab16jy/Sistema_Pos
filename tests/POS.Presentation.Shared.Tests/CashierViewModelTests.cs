using POS.Application.DTOs.Products;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CashierViewModelTests
{
    [Fact]
    public async Task ConfirmSaleAsync_WhenCartAndPaymentAreValid_PersistsSaleAndResetsCartAfterSuccess()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork))
        {
            SearchText = "rice",
            Quantity = 2m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 6_000m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.CartItems);
        Assert.Equal(0m, viewModel.TotalAmount);
        Assert.Equal(0m, viewModel.ReceivedAmount);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(5_000m, sale.Total().Amount);
        Assert.Equal(5_000m, sale.CashInDrawer().Amount);
        Assert.Equal(8m, product.CurrentStock);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenCartIsEmpty_DoesNotPersistAndShowsRecoverableMessage()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork()))
        {
            CashierUserId = Guid.NewGuid(),
            ReceivedAmount = 10_000m
        };

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sales.Sales);
        Assert.Contains("agrega", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_FindsProductsByNameAndSelectsFirstResult()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(viewModel.SearchResults);
        Assert.Equal("Arroz 500g", viewModel.SelectedProduct?.Name);
        Assert.Contains("Selecciona", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSelectedProduct_AddsItemAndCalculatesTotal()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "aceite",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Single(viewModel.CartItems);
        Assert.Equal(19600m, viewModel.TotalAmount);
        Assert.Equal("$19.600", viewModel.TotalText);
        Assert.Contains("agregado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceivedCash_WhenEnough_CalculatesChangeAndAllowsPrototypeConfirmation()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 10000m;

        Assert.True(viewModel.CanConfirm);
        Assert.Equal(5000m, viewModel.ChangeAmount);
        Assert.Equal("$5.000", viewModel.ChangeText);
    }

    [Fact]
    public async Task ReceivedCash_WhenLowerThanTotal_DisablesConfirmation()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 1000m;

        Assert.False(viewModel.CanConfirm);
        Assert.Equal(0m, viewModel.ChangeAmount);
        Assert.Contains("no cubre", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSelectedProduct_WhenQuantityExceedsStock_ShowsRecoveryMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 99m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Empty(viewModel.CartItems);
        Assert.Contains("Solo hay", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticProductLookupService : POS.Application.Interfaces.IProductLookupService
    {
        private readonly ProductLookupResult _product;

        public StaticProductLookupService(ProductLookupResult product) => _product = product;

        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([_product]);
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

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
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
