using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class NewPurchaseViewModelTests
{
    [Fact]
    public async Task LoadAsync_ListsOnlyActiveSuppliers()
    {
        var viewModel = CreateViewModel([
            new SupplierSearchResult(Guid.NewGuid(), "Acme Foods", "900123", null, null, true),
            new SupplierSearchResult(Guid.NewGuid(), "Cerrada SAS", "900999", null, null, false)
        ]);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        SupplierSearchResult supplier = Assert.Single(viewModel.Suppliers);
        Assert.Equal("Acme Foods", supplier.Name);
    }

    [Fact]
    public async Task SelectingSupplier_LoadsOnlyEligibleCatalogItems()
    {
        var supplierId = Guid.NewGuid();
        var eligible = CatalogItem(supplierId, "Rice");
        var notConfigured = new SupplierCatalogItemResult(supplierId, "Acme Foods", Guid.NewGuid(), "Beans", "BEANS-REF", null, null, null, null, true);
        var viewModel = CreateViewModel([ActiveSupplier(supplierId)], [eligible, notConfigured]);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = Assert.Single(viewModel.Suppliers);

        Assert.Equal(eligible, Assert.Single(viewModel.CatalogSuggestions));
        Assert.True(viewModel.HasSupplier);
        Assert.Contains("Acme Foods", viewModel.SupplierContextLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogSearchText_FiltersByNameReferenceAndSku_AndAutoSelectsFirstMatch()
    {
        var supplierId = Guid.NewGuid();
        var rice = CatalogItem(supplierId, "Rice", sku: "RICE-12", reference: "RICE-REF");
        var beans = CatalogItem(supplierId, "Beans", sku: "BEANS-9", reference: "BEANS-REF");
        var viewModel = await CreateLoadedViewModel([rice, beans], supplierId);

        viewModel.CatalogSearchText = "ric";
        Assert.Equal(rice, Assert.Single(viewModel.CatalogSuggestions));
        Assert.Equal(rice, viewModel.SelectedCatalogItem);

        viewModel.CatalogSearchText = "BEANS-9";
        Assert.Equal(beans, Assert.Single(viewModel.CatalogSuggestions));

        viewModel.CatalogSearchText = "BEANS-REF";
        Assert.Equal(beans, Assert.Single(viewModel.CatalogSuggestions));

        viewModel.CatalogSearchText = "zzz";
        Assert.Empty(viewModel.CatalogSuggestions);
        Assert.True(viewModel.HasNoCatalogMatches);
        Assert.Null(viewModel.SelectedCatalogItem);

        viewModel.CatalogSearchText = string.Empty;
        Assert.Equal(2, viewModel.CatalogSuggestions.Count);
        Assert.False(viewModel.HasNoCatalogMatches);
    }

    [Fact]
    public async Task MoveCatalogSelection_NavigatesAndClampsAtBounds()
    {
        var supplierId = Guid.NewGuid();
        var rice = CatalogItem(supplierId, "Rice");
        var beans = CatalogItem(supplierId, "Beans");
        var viewModel = await CreateLoadedViewModel([rice, beans], supplierId);

        viewModel.MoveCatalogSelection(1);
        Assert.Equal(rice, viewModel.SelectedCatalogItem);

        viewModel.MoveCatalogSelection(1);
        Assert.Equal(beans, viewModel.SelectedCatalogItem);

        viewModel.MoveCatalogSelection(1);
        Assert.Equal(beans, viewModel.SelectedCatalogItem);

        viewModel.MoveCatalogSelection(-1);
        Assert.Equal(rice, viewModel.SelectedCatalogItem);

        viewModel.MoveCatalogSelection(-1);
        Assert.Equal(rice, viewModel.SelectedCatalogItem);
    }

    [Fact]
    public async Task AddSelectedCatalogItem_AddsLineWithOverrideAndResetsEntryFields()
    {
        var supplierId = Guid.NewGuid();
        var viewModel = await CreateLoadedViewModel([CatalogItem(supplierId, cost: 12_000m)], supplierId);
        viewModel.CatalogSearchText = "Rice";
        viewModel.DraftQuantity = 2m;
        viewModel.UseDraftCostOverride = true;
        viewModel.DraftCostOverride = 13_000m;

        Assert.True(viewModel.AddSelectedCatalogItem());

        NewPurchaseLineViewModel line = Assert.Single(viewModel.Items);
        Assert.Equal("Bulto", line.PurchaseUnitName);
        Assert.Equal(12m, line.UnitsPerPurchaseUnit);
        Assert.Equal(13_000m, line.ConfirmedCost);
        Assert.Equal(24m, line.UnitsReceived);
        Assert.Equal(26_000m, line.Subtotal);
        Assert.Equal(string.Empty, viewModel.CatalogSearchText);
        Assert.Equal(viewModel.CatalogSuggestions.Single(), viewModel.SelectedCatalogItem);
        Assert.Contains("agregado a la compra", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1m, viewModel.DraftQuantity);
        Assert.False(viewModel.UseDraftCostOverride);
    }

    [Fact]
    public async Task AddSelectedCatalogItem_KeepsSupplierAndProductStepReadyForMultipleEligibleProducts()
    {
        var supplierId = Guid.NewGuid();
        var rice = CatalogItem(supplierId, "Rice");
        var beans = CatalogItem(supplierId, "Beans", sku: "BEANS-12", reference: "BEANS-REF");
        var oil = CatalogItem(supplierId, "Oil", sku: "OIL-12", reference: "OIL-REF");
        var viewModel = await CreateLoadedViewModel([rice, beans, oil], supplierId);
        SupplierSearchResult supplier = viewModel.SelectedSupplier!;
        viewModel.GoToStepCommand.Execute("2");

        foreach (SupplierCatalogItemResult item in new[] { rice, beans, oil })
        {
            viewModel.CatalogSearchText = item.ProductName;
            Assert.True(viewModel.AddSelectedCatalogItem());
        }

        Assert.Same(supplier, viewModel.SelectedSupplier);
        Assert.Equal(2, viewModel.CurrentStep);
        Assert.Equal(3, viewModel.Items.Count);
        Assert.Equal(string.Empty, viewModel.CatalogSearchText);
        Assert.Contains("Oil", viewModel.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddSelectedCatalogItem_BlocksDuplicateProduct()
    {
        var supplierId = Guid.NewGuid();
        var item = CatalogItem(supplierId);
        var viewModel = await CreateLoadedViewModel([item], supplierId);

        viewModel.SelectedCatalogItem = item;
        Assert.True(viewModel.AddSelectedCatalogItem());

        viewModel.SelectedCatalogItem = item;
        Assert.False(viewModel.AddSelectedCatalogItem());
        Assert.Single(viewModel.Items);
        Assert.Contains("ya está en la compra", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditingLineQuantityAndCost_RecalculatesTotalsImmediately()
    {
        var supplierId = Guid.NewGuid();
        var viewModel = await CreateLoadedViewModel([CatalogItem(supplierId, cost: 1_000m)], supplierId);
        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.AddSelectedCatalogItem();
        viewModel.VatRate = 19m;

        NewPurchaseLineViewModel line = viewModel.Items.Single();
        line.Quantity = 3m;
        line.ConfirmedCost = 2_000m;

        Assert.Equal(6_000m, viewModel.Subtotal);
        Assert.Equal(1_140m, viewModel.Iva);
        Assert.Equal(7_140m, viewModel.Total);
        Assert.Equal(36m, line.UnitsReceived);

        viewModel.VatRate = 0m;
        Assert.Equal(6_000m, viewModel.Total);
    }

    [Fact]
    public void VatRate_ClampsToValidPercentageRange()
    {
        var viewModel = CreateViewModel([]);

        viewModel.VatRate = 150m;
        Assert.Equal(100m, viewModel.VatRate);

        viewModel.VatRate = -5m;
        Assert.Equal(0m, viewModel.VatRate);
    }

    [Fact]
    public void LineInputs_ClampNegativeValuesToZeroAndMarkLineInvalid()
    {
        var line = new NewPurchaseLineViewModel(CatalogItem(Guid.NewGuid()), 2m, 1_000m);

        line.Quantity = -3m;

        Assert.Equal(0m, line.Quantity);
        Assert.False(line.IsValid);
    }

    [Fact]
    public async Task GoToStepCommand_ValidatesNavigationInViewModel()
    {
        var supplierId = Guid.NewGuid();
        var viewModel = CreateViewModel([ActiveSupplier(supplierId)], [CatalogItem(supplierId)]);

        Assert.False(viewModel.GoToStepCommand.CanExecute("2"));
        Assert.False(viewModel.GoToStepCommand.CanExecute("3"));
        viewModel.GoToStepCommand.Execute("2");
        Assert.Equal(1, viewModel.CurrentStep);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();

        Assert.True(viewModel.GoToStepCommand.CanExecute("2"));
        Assert.False(viewModel.GoToStepCommand.CanExecute("3"));
        viewModel.GoToStepCommand.Execute("2");
        Assert.True(viewModel.IsStep2Active);

        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.AddSelectedCatalogItem();

        Assert.True(viewModel.GoToStepCommand.CanExecute("3"));
        viewModel.GoToStepCommand.Execute("3");
        Assert.True(viewModel.IsStep3Active);
    }

    [Fact]
    public async Task ConfirmPurchaseCommand_GeneratesInvoiceAndStillRequiresCreditTerms()
    {
        var supplierId = Guid.NewGuid();
        var viewModel = await CreateLoadedViewModel([CatalogItem(supplierId)], supplierId);
        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.AddSelectedCatalogItem();

        Assert.True(viewModel.ConfirmPurchaseCommand.CanExecute(null));

        viewModel.PaymentMethod = "Crédito";
        Assert.False(viewModel.ConfirmPurchaseCommand.CanExecute(null));
        Assert.Contains("plazo", viewModel.ConfirmBlockReason, StringComparison.OrdinalIgnoreCase);

        viewModel.PaymentTerms = "30 días";
        Assert.True(viewModel.ConfirmPurchaseCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmPurchaseAsync_SendsValidatedRequestAndResetsFlow()
    {
        var supplierId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var purchaseUseCase = new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), supplierId, 26_000m, 1, 4_940m, "CMP-20260711-test"));
        var viewModel = CreateViewModel([ActiveSupplier(supplierId)], [CatalogItem(supplierId, cost: 12_000m)], purchaseUseCase, () => operatorId);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();
        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.DraftQuantity = 2m;
        viewModel.UseDraftCostOverride = true;
        viewModel.DraftCostOverride = 13_000m;
        viewModel.AddSelectedCatalogItem();
        viewModel.GoToStepCommand.Execute("3");
        viewModel.VatRate = 19m;

        await viewModel.ConfirmPurchaseAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(purchaseUseCase.LastRequest);
        Assert.Equal(supplierId, purchaseUseCase.LastRequest!.SupplierId);
        Assert.Equal(operatorId, purchaseUseCase.LastRequest.UserId);
        Assert.Null(purchaseUseCase.LastRequest.InvoiceNumber);
        Assert.Equal(19m, purchaseUseCase.LastRequest.VatRate);
        RegisterPurchaseItemRequest item = Assert.Single(purchaseUseCase.LastRequest.Items);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(13_000m, item.PurchaseUnitCostOverride);

        Assert.Empty(viewModel.Items);
        Assert.Equal("CMP-20260711-test", viewModel.LastGeneratedInvoiceNumber);
        Assert.Equal(1, viewModel.CurrentStep);
        Assert.Contains("CMP-20260711-test", viewModel.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmPurchaseAsync_WithoutManualInvoice_CallsUseCase()
    {
        var supplierId = Guid.NewGuid();
        var purchaseUseCase = new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), supplierId, 0m, 0));
        var viewModel = CreateViewModel([ActiveSupplier(supplierId)], [CatalogItem(supplierId)], purchaseUseCase);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();
        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.AddSelectedCatalogItem();

        await viewModel.ConfirmPurchaseAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(purchaseUseCase.LastRequest);
        Assert.Null(purchaseUseCase.LastRequest!.InvoiceNumber);
    }

    [Fact]
    public async Task ChangeSupplierCommand_ClearsDraftAndReturnsToStepOne()
    {
        var supplierId = Guid.NewGuid();
        var viewModel = await CreateLoadedViewModel([CatalogItem(supplierId)], supplierId);
        viewModel.SelectedCatalogItem = viewModel.CatalogSuggestions.Single();
        viewModel.AddSelectedCatalogItem();
        viewModel.GoToStepCommand.Execute("2");

        viewModel.ChangeSupplierCommand.Execute(null);

        Assert.Null(viewModel.SelectedSupplier);
        Assert.False(viewModel.HasSupplier);
        Assert.Empty(viewModel.Items);
        Assert.Empty(viewModel.CatalogSuggestions);
        Assert.Equal(1, viewModel.CurrentStep);
        Assert.False(viewModel.ChangeSupplierCommand.CanExecute(null));
    }

    private static async Task<NewPurchaseViewModel> CreateLoadedViewModel(IReadOnlyList<SupplierCatalogItemResult> catalogItems, Guid supplierId)
    {
        var viewModel = CreateViewModel([ActiveSupplier(supplierId)], catalogItems);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();
        return viewModel;
    }

    private static NewPurchaseViewModel CreateViewModel(
        IReadOnlyList<SupplierSearchResult> suppliers,
        IReadOnlyList<SupplierCatalogItemResult>? catalogItems = null,
        StaticRegisterPurchaseUseCase? purchaseUseCase = null,
        Func<Guid>? currentOperatorIdProvider = null)
    {
        GetEligibleSupplierCatalogUseCase? eligibleCatalog = catalogItems is null
            ? null
            : new GetEligibleSupplierCatalogUseCase(new StaticSupplierCatalogQueryService(catalogItems));
        return new NewPurchaseViewModel(
            new SearchSuppliersUseCase(new StaticSupplierLookupQueryService(suppliers)),
            purchaseUseCase ?? new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), Guid.NewGuid(), 0m, 0)),
            currentOperatorIdProvider ?? (() => Guid.NewGuid()),
            eligibleCatalog);
    }

    private static SupplierSearchResult ActiveSupplier(Guid? id = null)
        => new(id ?? Guid.NewGuid(), "Acme Foods", "900123", null, null, true);

    private static SupplierCatalogItemResult CatalogItem(
        Guid supplierId, string name = "Rice", string? sku = "RICE-12", string? reference = "RICE-REF", decimal cost = 12_000m)
        => new(supplierId, "Acme Foods", Guid.NewGuid(), name, reference, sku, "Bulto", 12m, cost, true);

    private sealed class StaticSupplierLookupQueryService : ISupplierLookupQueryService
    {
        private readonly IReadOnlyList<SupplierSearchResult> _suppliers;

        public StaticSupplierLookupQueryService(IReadOnlyList<SupplierSearchResult> suppliers) => _suppliers = suppliers;

        public Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.IncludeInactive ? _suppliers : _suppliers.Where(supplier => supplier.IsActive).ToList());
    }

    private sealed class StaticSupplierCatalogQueryService : ISupplierCatalogQueryService
    {
        private readonly IReadOnlyList<SupplierCatalogItemResult> _items;

        public StaticSupplierCatalogQueryService(IReadOnlyList<SupplierCatalogItemResult> items) => _items = items;

        public Task<IReadOnlyList<SupplierCatalogItemResult>> GetForSupplierAsync(Guid supplierId, bool eligibleOnly, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SupplierCatalogItemResult>>(_items.Where(item => item.SupplierId == supplierId && (!eligibleOnly || item.IsEligibleForPurchase)).ToList());

        public Task<IReadOnlyList<SupplierCatalogItemResult>> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SupplierCatalogItemResult>>(_items.Where(item => item.ProductId == productId).ToList());
    }

    private sealed class StaticRegisterPurchaseUseCase : RegisterPurchaseUseCase
    {
        private readonly RegisterPurchaseResult _result;

        public StaticRegisterPurchaseUseCase(RegisterPurchaseResult result)
            : base(new NullSupplierRepository(), new NullProductRepository(), new NullCatalogRepository(), new NullPurchaseRepository(), new InMemoryUnitOfWork())
            => _result = result;

        public RegisterPurchaseRequest? LastRequest { get; private set; }

        public override Task<RegisterPurchaseResult> ExecuteAsync(RegisterPurchaseRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class NullSupplierRepository : ISupplierRepository
    {
        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPurchasesAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullProductRepository : IProductRepository
    {
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
    }

    private sealed class NullCatalogRepository : IProductSupplierRepository
    {
        public Task AddAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProductSupplier?> GetAsync(Guid supplierId, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductSupplier?>(null);
        public Task<IReadOnlyList<ProductSupplier>> GetForSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductSupplier>>([]);
        public Task<IReadOnlyList<ProductSupplier>> GetActiveForSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductSupplier>>([]);
        public Task UpdateAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullPurchaseRepository : IPurchaseRepository
    {
        public Task AddAsync(Purchase purchase, IReadOnlyCollection<InventoryMovement> movements, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
