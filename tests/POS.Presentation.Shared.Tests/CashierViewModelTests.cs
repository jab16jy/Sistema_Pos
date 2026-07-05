using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Cashier;
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
        var printService = new StubPrintService();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork),
            null,
            printService)
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
        Assert.Contains("compra realizada exitosamente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("impresa", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(sale.Id, printService.LastSaleId);
        Assert.Equal(5_000m, sale.Total().Amount);
        Assert.Equal(5_000m, sale.CashInDrawer().Amount);
        Assert.Equal(8m, product.CurrentStock);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WhenBarcodeMatches_AddsProductAndClearsSearch()
    {
        var product = new ProductLookupResult(Guid.NewGuid(), "Rice", "7701", 2_500m, 10m);
        var viewModel = new CashierViewModel(new StaticProductLookupService(product))
        {
            SearchText = "7701"
        };

        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        CashierCartItemViewModel item = Assert.Single(viewModel.CartItems);
        Assert.Equal("Rice", item.ProductName);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Empty(viewModel.SearchResults);
        Assert.Contains("escáner", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenPrintingFails_StillShowsSuccessfulPurchaseMessage()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork()),
            null,
            new StubPrintService { ErrorToThrow = new InvalidOperationException("Printer offline") })
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Contains("compra realizada exitosamente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no se pudo imprimir", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(sales.Sales);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenPrintReceiptIsDisabled_SkipsPrintingAndShowsSuccessMessage()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var printService = new StubPrintService();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork()),
            null,
            printService)
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid(),
            ShouldPrintReceipt = false
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Contains("compra realizada exitosamente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin imprimir recibo", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(printService.LastSaleId);
        Assert.True(viewModel.ShouldPrintReceipt);
        Assert.Single(sales.Sales);
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
        Assert.Contains("El carrito está vacío", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task SearchAsync_ExposesPresentationStockStatusForDropdown()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(Guid.NewGuid(), "Low stock item", "LOW-1", 1_500m, 4m, 5m)))
        {
            SearchText = "low"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Equal("Stock bajo", result.StockStatusLabel);
        Assert.Equal("warning", result.StockStatusKind);
        Assert.Equal("$1.500", result.UnitPriceText);
    }

    [Fact]
    public async Task CartItemQuantityChange_RecomputesSubtotalSummaryAndPaymentBalance()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 10_000m;
        CashierCartItemViewModel item = Assert.Single(viewModel.CartItems);

        item.Quantity = 3m;

        Assert.Equal(7_500m, item.Subtotal);
        Assert.Equal(7_500m, viewModel.TotalAmount);
        Assert.Equal("1 productos · 3 unidades", viewModel.CartSummaryText);
        Assert.Equal("$2.500", viewModel.PaymentBalanceText);
    }

    [Fact]
    public async Task SecondaryActions_ArePresentationOnlyAndExposeRecoveryMessages()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.True(viewModel.AssociateCustomerCommand.CanExecute(null));
        Assert.True(viewModel.HoldSaleCommand.CanExecute(null));

        viewModel.AssociateCustomerCommand.Execute(null);
        Assert.Contains("cliente", viewModel.Message, StringComparison.OrdinalIgnoreCase);

        viewModel.HoldSaleCommand.Execute(null);
        Assert.Contains("Requiere un caso de uso", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.True(viewModel.IsPaymentShortfall);
        Assert.Equal("Falta", viewModel.PaymentBalanceLabel);
        Assert.Equal("$10.200", viewModel.PaymentBalanceText);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenCartIsEmpty_ShowsWireframeEmptyCartMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            ReceivedAmount = 10_000m
        };

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Contains("El carrito está vacío", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Sales.FirstOrDefault(sale => sale.Id == id));

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    // --- RED: settlement plan / deferred balance in CashierViewModel ---

    [Fact]
    public async Task DeferredAmount_WhenSetAndProductInCart_PreviewShowsDeferredCreditMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid()
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Set deferred amount so that CollectedAmount + DeferredAmount == Total
        viewModel.DeferredAmount = viewModel.TotalAmount;
        viewModel.ReceivedAmount = 0m;

        Assert.True(viewModel.CanConfirm);
        Assert.Equal(viewModel.TotalAmount, viewModel.DeferredAmount);
        Assert.Contains("crédito", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithDeferredBalance_WhenNoCustomerSet_CannotConfirm()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
            // CustomerId not set
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Attempt deferred sale without a customer
        viewModel.DeferredAmount = viewModel.TotalAmount;
        viewModel.ReceivedAmount = 0m;

        // Without a customer, deferred should block confirmation
        Assert.False(viewModel.CanConfirm);
    }

    [Fact]
    public async Task SettlementPlan_WhenCollectedAmountPlusDeferredEqualsTotal_CanConfirm()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m,
            CustomerId = Guid.NewGuid()
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        decimal total = viewModel.TotalAmount;
        // Pay half in cash, defer half
        viewModel.ReceivedAmount = total / 2m;
        viewModel.DeferredAmount = total / 2m;

        Assert.True(viewModel.CanConfirm);
        Assert.Contains("crédito", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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

    private sealed class StubPrintService : IPrintService
    {
        public Guid? LastSaleId { get; private set; }
        public Exception? ErrorToThrow { get; init; }

        public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
        {
            if (ErrorToThrow is not null)
            {
                throw ErrorToThrow;
            }

            LastSaleId = saleId;
            return Task.CompletedTask;
        }
    }
}
