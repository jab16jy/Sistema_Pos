using POS.Application.DTOs.Products;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class PurchasesViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsActiveSuppliersForPurchaseSelection()
    {
        var suppliers = new StaticSupplierLookupQueryService([
            new SupplierSearchResult(Guid.NewGuid(), "Acme Foods", "900123", null, null, true)
        ]);
        var viewModel = CreateViewModel(suppliers);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        SupplierSearchResult supplier = Assert.Single(viewModel.Suppliers);
        Assert.Equal("Acme Foods", supplier.Name);
        Assert.Contains("Selecciona", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveSupplierAsync_WithValidData_RefreshesSuppliersWithoutRepositoryDependency()
    {
        var repository = new InMemorySupplierRepository();
        var viewModel = CreateViewModel(new InMemorySupplierLookupQueryService(repository), repository);
        viewModel.SupplierName = "Acme Foods";
        viewModel.SupplierTaxId = "900123";

        await viewModel.SaveSupplierAsync(TestContext.Current.CancellationToken);

        SupplierSearchResult supplier = Assert.Single(viewModel.Suppliers);
        Assert.Equal("Acme Foods", supplier.Name);
        Assert.Equal(string.Empty, viewModel.SupplierName);
        Assert.Contains("guardado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectedSupplier_SaveAndCancel_UsesEditingIdentityWithoutCreatingDuplicate()
    {
        var repository = new InMemorySupplierRepository();
        var save = new SaveSupplierUseCase(repository, new InMemoryUnitOfWork());
        SupplierResult supplier = await save.ExecuteAsync(new SaveSupplierRequest(null, "Acme Foods", "900123", null, null, true), TestContext.Current.CancellationToken);
        var viewModel = CreateViewModel(new InMemorySupplierLookupQueryService(repository), repository);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = Assert.Single(viewModel.Suppliers);
        viewModel.SupplierName = "Acme Wholesale";
        await viewModel.SaveSupplierAsync(TestContext.Current.CancellationToken);

        Assert.Equal(supplier.Id, viewModel.EditingSupplierId);
        Assert.Single(repository.Suppliers);
        Assert.Equal("Acme Wholesale", repository.Suppliers.Single().Name);
        viewModel.CancelSupplierEditCommand.Execute(null);
        Assert.Null(viewModel.EditingSupplierId);
        Assert.Null(viewModel.SelectedSupplier);
        Assert.Equal(string.Empty, viewModel.SupplierName);
    }

    [Fact]
    public void SelectingCatalogRelationship_PrefillsEditorForUpdate()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([]));
        var catalogItem = new SupplierCatalogItemResult(
            Guid.NewGuid(), "Acme Foods", Guid.NewGuid(), "Rice", "RICE-REF", "RICE-12", "Bulto", 12m, 12_000m, false);

        viewModel.SelectedProfileCatalogItem = catalogItem;

        Assert.Equal(catalogItem.ProductId, viewModel.SelectedCatalogProduct?.ProductId);
        Assert.Equal("RICE-12", viewModel.CatalogSupplierSku);
        Assert.Equal("Bulto", viewModel.CatalogPurchaseUnit);
        Assert.Equal(12m, viewModel.CatalogFactor);
        Assert.Equal(12_000m, viewModel.CatalogCost);
        Assert.False(viewModel.CatalogIsActive);
    }

    [Fact]
    public async Task SelectingSupplier_LoadsFullProfileCatalogIncludingInactiveItems()
    {
        var supplierId = Guid.NewGuid();
        var activeItem = new SupplierCatalogItemResult(
            supplierId, "Acme Foods", Guid.NewGuid(), "Rice", "RICE-REF", "RICE-12", "Bulto", 12m, 12_000m, true);
        var inactiveItem = new SupplierCatalogItemResult(
            supplierId, "Acme Foods", Guid.NewGuid(), "Beans", "BEANS-REF", null, "Bulto", 12m, 10_000m, false);
        var viewModel = CreateViewModel(
            new StaticSupplierLookupQueryService([new SupplierSearchResult(supplierId, "Acme Foods", "900123", null, null, true)]),
            catalogQuery: new StaticSupplierCatalogQueryService([activeItem, inactiveItem]));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = Assert.Single(viewModel.Suppliers);

        Assert.Equal(2, viewModel.SupplierCatalogItems.Count);
    }

    [Fact]
    public async Task LoadAsync_AlsoLoadsSuppliersForTheNewPurchaseFlow()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([
            new SupplierSearchResult(Guid.NewGuid(), "Acme Foods", "900123", null, null, true)
        ]));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.NewPurchase.Suppliers);
    }

    [Fact]
    public void NewPurchaseMessages_SurfaceOnTheSharedStatusBar()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([]));

        viewModel.NewPurchase.AddSelectedCatalogItem();

        Assert.Equal(viewModel.NewPurchase.Message, viewModel.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenSupplierLookupFails_SetsRecoverableMessage()
    {
        var viewModel = CreateViewModel(new ThrowingSupplierLookupQueryService());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar proveedores", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectingCatalogProduct_PreFillsEditorWithDefaults()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([]));
        var productLookup = new ProductLookupResult(Guid.NewGuid(), "Rice", "RICE-123", 10_000m, 100m);

        viewModel.SelectedCatalogProduct = productLookup;

        Assert.Equal("RICE-123", viewModel.CatalogSupplierSku);
        Assert.Equal("Unidad", viewModel.CatalogPurchaseUnit);
        Assert.Equal(1m, viewModel.CatalogFactor);
        Assert.Equal(7000m, viewModel.CatalogCost);
        Assert.True(viewModel.CatalogIsActive);
    }

    [Fact]
    public void Tabs_SeparateNewPurchaseAndHistoryAreas()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([]));
        viewModel.ActiveTab = "History";
        Assert.True(viewModel.IsHistoryTabActive);
        Assert.False(viewModel.IsNewPurchaseTabActive);
    }

    [Fact]
    public void WpfComposition_UsesCashierOperatorSeamForPurchaseAudit()
    {
        string bootstrapperSource = File.ReadAllText(FindRepositoryFile("src/POS.Presentation/AppBootstrapper.cs"));

        Assert.DoesNotContain("LocalPrototypeOperatorId", bootstrapperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", bootstrapperSource, StringComparison.Ordinal);
        Assert.Contains("CashierUserId", bootstrapperSource, StringComparison.Ordinal);
        Assert.Contains("() => cashier.CashierUserId", bootstrapperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainShell_ExposesPurchasesModuleAsImplementedWithoutActivatingReceivablesOrReports()
    {
        var shell = new MainShellViewModel { Purchases = CreateViewModel(new StaticSupplierLookupQueryService([])) };
        var suppliers = shell.AdminHubModules.Single(module => module.Key == "suppliers");
        var reports = shell.AdminHubModules.Single(module => module.Key == "reports");

        shell.SelectedModule = suppliers;

        Assert.True(suppliers.IsImplemented);
        Assert.True(shell.IsPurchasesSelected);
        Assert.True(shell.IsAdminSectionSelected);
        // WU4: reports module is now implemented (IsImplemented: true); IsPlannedModuleSelected remains false
        Assert.True(reports.IsImplemented);
        Assert.False(shell.IsPlannedModuleSelected);
    }

    private static PurchasesViewModel CreateViewModel(
        ISupplierLookupQueryService supplierLookup,
        InMemorySupplierRepository? repository = null,
        StaticRegisterPurchaseUseCase? purchaseUseCase = null,
        Func<Guid>? currentOperatorIdProvider = null,
        IProductLookupService? productLookup = null,
        ISupplierCatalogQueryService? catalogQuery = null)
    {
        repository ??= new InMemorySupplierRepository();
        GetSupplierCatalogUseCase? catalog = catalogQuery is null ? null : new GetSupplierCatalogUseCase(catalogQuery);
        GetEligibleSupplierCatalogUseCase? eligibleCatalog = catalogQuery is null ? null : new GetEligibleSupplierCatalogUseCase(catalogQuery);
        return new PurchasesViewModel(
            new SaveSupplierUseCase(repository, new InMemoryUnitOfWork()),
            new SearchSuppliersUseCase(supplierLookup),
            purchaseUseCase ?? new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), Guid.NewGuid(), 0m, 0)),
            currentOperatorIdProvider ?? (() => Guid.NewGuid()),
            productLookup,
            catalog,
            eligibleCatalog);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }

    private sealed class InMemorySupplierRepository : ISupplierRepository
    {
        public List<Supplier> Suppliers { get; } = [];

        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            Suppliers.Add(supplier);
            return Task.CompletedTask;
        }

        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id));

        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasPurchasesAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Suppliers.RemoveAll(supplier => supplier.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySupplierLookupQueryService : ISupplierLookupQueryService
    {
        private readonly InMemorySupplierRepository _repository;

        public InMemorySupplierLookupQueryService(InMemorySupplierRepository repository) => _repository = repository;

        public Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
        {
            IEnumerable<Supplier> query = _repository.Suppliers;
            if (!request.IncludeInactive)
            {
                query = query.Where(supplier => supplier.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                query = query.Where(supplier => supplier.Name.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase) || supplier.TaxId.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<SupplierSearchResult>>(query.Select(SupplierSearchResult.FromSupplier).ToList());
        }
    }

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

    private sealed class ThrowingSupplierLookupQueryService : ISupplierLookupQueryService
    {
        public Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló proveedores");
    }

    private sealed class StaticRegisterPurchaseUseCase : RegisterPurchaseUseCase
    {
        private readonly RegisterPurchaseResult _result;

        public StaticRegisterPurchaseUseCase(RegisterPurchaseResult result) : base(new NullSupplierRepository(), new NullProductRepository(), new NullPurchaseRepository(), new InMemoryUnitOfWork())
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

    private sealed class NullPurchaseRepository : IPurchaseRepository
    {
        public Task AddAsync(Purchase purchase, IReadOnlyCollection<InventoryMovement> movements, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

}
