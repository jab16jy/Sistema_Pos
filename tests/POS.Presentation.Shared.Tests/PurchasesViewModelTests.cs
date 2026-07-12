using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class PurchasesViewModelTests
{
    [Fact]
    public async Task SelectingSupplier_LoadsOnlyItsActivePurchaseCatalogAndAddsQuantityOnlyDraft()
    {
        Guid supplierId = Guid.NewGuid();
        var catalog = new StaticCatalogQuery([new SupplierCatalogItemResult(supplierId, Guid.NewGuid(), "Ampolla", "A-1", "SUP-1", "Caja", 12m, 24_000m, true)]);
        var viewModel = CreateViewModel(new StaticSupplierLookup([new SupplierSearchResult(supplierId, "Acme", "900", null, null, true)]), catalog: new GetSupplierCatalogUseCase(catalog));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = Assert.Single(viewModel.Suppliers);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        viewModel.SelectedCatalogItem = Assert.Single(viewModel.CatalogItems);
        viewModel.DraftQuantity = 2m;
        viewModel.AddDraftItemCommand.Execute(null);

        PurchaseDraftItem item = Assert.Single(viewModel.Items);
        Assert.Equal(24m, item.InventoryUnitsReceived);
        Assert.Equal(48_000m, item.Subtotal);
    }

    [Fact]
    public async Task RegisterPurchase_CarriesAuditableHeaderAndCatalogQuantities()
    {
        Guid supplierId = Guid.NewGuid();
        var register = new CapturingRegisterPurchaseUseCase(supplierId);
        var viewModel = CreateViewModel(new StaticSupplierLookup([new SupplierSearchResult(supplierId, "Acme", "900", null, null, true)]), register);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();
        viewModel.PurchaseInvoiceNumber = "FAC-3";
        viewModel.PurchaseDate = new DateTime(2026, 7, 11);
        viewModel.Items.Add(new PurchaseDraftItem(Guid.NewGuid(), "Rice", "R", "Bulto", 25m, 100_000m, 2m));

        await viewModel.RegisterPurchaseAsync(TestContext.Current.CancellationToken);

        Assert.Equal("FAC-3", register.LastRequest?.InvoiceNumber);
        Assert.Equal(2m, register.LastRequest?.Items.Single().Quantity);
        Assert.Contains("persistido", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tabs_SeparateNewPurchaseHistoryAndSupplierQuotationAreas()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookup([]));
        viewModel.ActiveTab = "History";
        Assert.True(viewModel.IsHistoryTabActive);
        viewModel.ActiveTab = "Quotation";
        Assert.True(viewModel.IsQuotationTabActive);
        Assert.False(viewModel.IsNewPurchaseTabActive);
    }

    private static PurchasesViewModel CreateViewModel(ISupplierLookupQueryService supplierLookup, RegisterPurchaseUseCase? register = null, GetSupplierCatalogUseCase? catalog = null)
        => new(new SaveSupplierUseCase(new NullSupplierRepository(), new InMemoryUnitOfWork()), new SearchSuppliersUseCase(supplierLookup), register ?? new CapturingRegisterPurchaseUseCase(Guid.NewGuid()), () => Guid.NewGuid(), catalog);

    private sealed class StaticSupplierLookup(IReadOnlyList<SupplierSearchResult> suppliers) : ISupplierLookupQueryService
    {
        public Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(suppliers);
    }

    private sealed class StaticCatalogQuery(IReadOnlyList<SupplierCatalogItemResult> catalog) : ISupplierCatalogQueryService
    {
        public Task<IReadOnlyList<SupplierCatalogItemResult>> GetActiveAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult(catalog);
    }

    private sealed class CapturingRegisterPurchaseUseCase(Guid supplierId) : RegisterPurchaseUseCase(new NullSupplierRepository(), new NullProductRepository(), new NullPurchaseRepository(), new InMemoryUnitOfWork())
    {
        public RegisterPurchaseRequest? LastRequest { get; private set; }
        public override Task<RegisterPurchaseResult> ExecuteAsync(RegisterPurchaseRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new RegisterPurchaseResult(Guid.NewGuid(), supplierId, 1m, request.Items.Count));
        }
    }

    private sealed class NullSupplierRepository : ISupplierRepository
    {
        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class NullProductRepository : IProductRepository
    {
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
    }
    private sealed class NullPurchaseRepository : IPurchaseRepository
    {
        public Task AddAsync(Purchase purchase, IReadOnlyCollection<InventoryMovement> movements, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Purchase?>(null);
    }
    private sealed class InMemoryUnitOfWork : IUnitOfWork { public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1); }
}
