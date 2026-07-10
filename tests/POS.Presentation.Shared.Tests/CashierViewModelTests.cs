using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Quotes;
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
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork))
        {
            SearchText = "rice",
            Quantity = 2m,
            CashierUserId = Guid.NewGuid()
        };
        int completedSales = 0;
        viewModel.SaleCompleted += () => completedSales++;

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 6_000m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.CartItems);
        Assert.Equal(0m, viewModel.TotalAmount);
        Assert.Equal(0m, viewModel.ReceivedAmount);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(5_000m, sale.Total().Amount);
        Assert.Equal(5_000m, sale.CashInDrawer().Amount);
        Assert.Equal(8m, product.CurrentStock);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, completedSales);
    }

    [Fact]
    public async Task AddingAsBox_ChargesBoxPrice_AndDecrementsStockInBaseUnits()
    {
        // Ampolla 350 (doc example): 1 unit = 10.000, but 1 box of 12 = 110.000 (not 120.000).
        var product = new Product(
            "Ampolla 350",
            Guid.NewGuid(),
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(24m),
            new StockQuantity(0m),
            unitsPerBox: 12,
            boxPrice: new Money(110_000m));
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(
                product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock, 0m, 12, 110_000m)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork))
        {
            SearchText = "ampolla",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.CanSellSelectedAsBox);

        viewModel.SellSelectedAsBox = true;
        viewModel.AddSelectedProductCommand.Execute(null);

        CashierCartItemViewModel line = Assert.Single(viewModel.CartItems);
        Assert.Equal(110_000m, line.UnitPrice);
        Assert.Equal(ProductPresentation.Box, line.Presentation);
        Assert.Equal(12m, line.InventoryUnits);

        viewModel.ReceivedAmount = 110_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(110_000m, sale.Total().Amount);
        Assert.Equal(12m, product.CurrentStock);
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
    public async Task SearchText_WhenTyped_AutocompletesProductsWithoutPressingSearch()
    {
        var lookup = new StaticProductLookupService(new ProductLookupResult(
            Guid.NewGuid(),
            "Exfoliante facial",
            "EXF-001",
            25_000m,
            8m));
        var viewModel = new CashierViewModel(lookup)
        {
            SearchText = "exfoliante"
        };

        await Task.Delay(350, TestContext.Current.CancellationToken);

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Equal("Exfoliante facial", result.Name);
        Assert.Equal(result, viewModel.SelectedProduct);
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
    public async Task ConfirmSaleAsync_WithoutCustomer_OpensAssignmentBeforePersistingAndCanContinueWithoutCustomer()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([])))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sales.Sales);
        Assert.True(viewModel.IsCustomerAssignmentOpen);
        Assert.Contains("Antes de facturar", viewModel.Message, StringComparison.OrdinalIgnoreCase);

        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Null(sale.CustomerId);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithSelectedCustomer_PersistsCustomerIdForHistoryAuditAndPoints()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        viewModel.CustomerSearchText = "Ana";
        await viewModel.SearchCustomersAsync(TestContext.Current.CancellationToken);
        CustomerSearchResult selected = Assert.Single(viewModel.CustomerSearchResults);

        await viewModel.SelectCustomerAsync(selected, TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(customer.Id, sale.CustomerId);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task AddProductAndResetSearch_AddsToCartAndClearsSearch()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        CashierProductSearchResultViewModel product = viewModel.SearchResults[0];

        bool added = viewModel.AddProductAndResetSearch(product);

        Assert.True(added);
        Assert.Single(viewModel.CartItems);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Empty(viewModel.SearchResults);
        Assert.Null(viewModel.SelectedProduct);
        Assert.Contains("agregado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddProductAndResetSearch_WhenStockInsufficient_KeepsSearchState()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 99m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        CashierProductSearchResultViewModel product = viewModel.SearchResults[0];

        bool added = viewModel.AddProductAndResetSearch(product);

        Assert.False(added);
        Assert.Empty(viewModel.CartItems);
        Assert.Equal("café", viewModel.SearchText);
        Assert.NotEmpty(viewModel.SearchResults);
        Assert.Contains("Solo hay", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveSelection_NavigatesAndClampsWithinResults()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "a"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.SearchResults.Count >= 2);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);

        viewModel.MoveSelection(1);
        Assert.Equal(viewModel.SearchResults[1], viewModel.SelectedProduct);

        viewModel.MoveSelection(-1);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);

        viewModel.MoveSelection(-1);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);
    }

    [Fact]
    public async Task SearchAsync_WhenNoResults_SetsHasNoMatches()
    {
        var viewModel = new CashierViewModel(new EmptyProductLookupService())
        {
            SearchText = "zzz"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasNoMatches);

        viewModel.SearchText = string.Empty;
        Assert.False(viewModel.HasNoMatches);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WithMissingCode_ShowsMessageAndLeavesCartUnchanged()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "MISSING-001"
        };

        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.CartItems);
        Assert.Equal("MISSING-001", viewModel.SearchText);
        Assert.Contains("No encontramos un producto activo", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WithExactBarcode_UsesFreshLookupAndAddsOnlyTheExactProduct()
    {
        var first = new ProductLookupResult(Guid.NewGuid(), "Arroz 500g", "ARR-1", 5_000m, 10m);
        var second = new ProductLookupResult(Guid.NewGuid(), "Azúcar 500g", "AZU-1", 4_000m, 10m);
        var viewModel = new CashierViewModel(new MultiProductLookupService(first, second))
        {
            SearchText = "AZU-1"
        };
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Azúcar 500g", Assert.Single(viewModel.CartItems).ProductName);
        Assert.Equal(string.Empty, viewModel.SearchText);
    }

    [Fact]
    public async Task ConfirmCustomerSelectionAsync_AssignsKeyboardSelectedCustomer()
    {
        var first = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var second = new CustomerSearchResult(Guid.NewGuid(), "Beatriz Cliente", "1002", "301", null, null, true);
        var viewModel = new CashierViewModel(
            new PrototypeProductLookupService(),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([first, second])))
        {
            CustomerSearchText = "cliente"
        };

        await viewModel.SearchCustomersAsync(TestContext.Current.CancellationToken);
        viewModel.MoveCustomerSelection(1);
        await viewModel.ConfirmCustomerSelectionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(second.Id, viewModel.SelectedCustomer?.Id);
    }

    [Fact]
    public async Task SelectCustomerAsync_WhenCustomerHasOpenQuote_ShowsPendingQuotesForCashier()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-001", customer.Id, productId, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        CustomerPayableQuote pending = Assert.Single(viewModel.CustomerQuotes);
        Assert.Equal("COT-CAJA-001", pending.QuoteNumber);
        Assert.True(viewModel.HasCustomerQuotes);
        Assert.Contains("cotización", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCustomerQuoteAsync_LoadsQuoteItemsIntoCashierCartWithDiscountedTotal()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-002", customer.Id, productId, discount: 10m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);

        CashierCartItemViewModel item = Assert.Single(viewModel.CartItems);
        Assert.Equal("Serum", item.ProductName);
        Assert.Equal(10m, item.Discount);
        Assert.Equal(1_800m, viewModel.TotalAmount);
        Assert.True(viewModel.IsQuoteLoaded);
        Assert.Contains("COT-CAJA-002", viewModel.LoadedQuoteBanner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenQuoteLoaded_ConvertsOriginalQuoteAndCreatesInvoice()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var product = new Product("Serum", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(10m), new StockQuantity(2m));
        var quote = CreateCustomerQuote("COT-CAJA-003", customer.Id, product.Id, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var createSaleUseCase = new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "SER-1", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            new ConvertQuoteToSaleUseCase(quoteRepo, createSaleUseCase, unitOfWork))
        {
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(sale.Id, quote.ConvertedSaleId);
        Assert.Empty(viewModel.CartItems);
        Assert.Contains("facturada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EmptyProductLookupService : POS.Application.Interfaces.IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([]);
    }

    private sealed class StaticProductLookupService : POS.Application.Interfaces.IProductLookupService
    {
        private readonly ProductLookupResult _product;

        public StaticProductLookupService(ProductLookupResult product) => _product = product;

        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([_product]);
    }

    private sealed class MultiProductLookupService(params ProductLookupResult[] products) : POS.Application.Interfaces.IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>(products);
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

    private sealed class StaticCustomerLookupQueryService : ICustomerLookupQueryService
    {
        private readonly IReadOnlyList<CustomerSearchResult> _customers;

        public StaticCustomerLookupQueryService(IReadOnlyList<CustomerSearchResult> customers) => _customers = customers;

        public Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_customers);
    }

    private static Quote CreateCustomerQuote(string quoteNumber, Guid customerId, Guid productId, decimal discount)
    {
        var quote = new Quote(
            quoteNumber,
            "Ana Cliente",
            30,
            customerId,
            customerPhone: "+57 300 123 4567",
            customerEmail: null);
        quote.AddItem(new QuoteItem(productId, "Serum", new Money(2_000m), new Quantity(1m), discount));
        return quote;
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly Dictionary<Guid, Quote> _quotes = new();

        public InMemoryQuoteRepository(params Quote[] quotes)
        {
            foreach (Quote quote in quotes)
            {
                _quotes[quote.Id] = quote;
            }
        }

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_quotes.TryGetValue(id, out Quote? quote) ? quote : null);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
        {
            _quotes[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default)
        {
            _quotes[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Quote>, int)>(([.. _quotes.Values], _quotes.Count));

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Quote>>(_quotes.Values
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
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
}
