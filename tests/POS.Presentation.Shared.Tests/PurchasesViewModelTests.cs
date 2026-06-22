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
    public async Task RegisterPurchaseAsync_WithSelectedSupplierAndLine_ShowsPersistedPurchaseMessage()
    {
        var supplierId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var purchaseUseCase = new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), supplierId, 12_000m, 1));
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([
            new SupplierSearchResult(supplierId, "Acme Foods", "900123", null, null, true)
        ]), purchaseUseCase: purchaseUseCase, currentOperatorIdProvider: () => operatorId);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedSupplier = viewModel.Suppliers.Single();
        viewModel.DraftProductId = Guid.NewGuid().ToString();
        viewModel.DraftQuantity = 3m;
        viewModel.DraftTotalCost = 12_000m;
        viewModel.AddDraftItemCommand.Execute(null);

        await viewModel.RegisterPurchaseAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.Items);
        Assert.Contains("Compra registrada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(supplierId, purchaseUseCase.LastRequest?.SupplierId);
        Assert.Equal(operatorId, purchaseUseCase.LastRequest?.UserId);
        Assert.Equal(3m, purchaseUseCase.LastRequest?.Items.Single().Quantity);
    }

    [Fact]
    public void AddDraftItemCommand_UpdatesCanExecuteWhenDraftInputChanges()
    {
        var viewModel = CreateViewModel(new StaticSupplierLookupQueryService([]));
        var changeCount = 0;
        viewModel.AddDraftItemCommand.CanExecuteChanged += (_, _) => changeCount++;

        Assert.False(viewModel.AddDraftItemCommand.CanExecute(null));

        viewModel.DraftProductId = Guid.NewGuid().ToString();
        viewModel.DraftQuantity = 2m;
        viewModel.DraftTotalCost = 5_000m;

        Assert.True(viewModel.AddDraftItemCommand.CanExecute(null));
        Assert.True(changeCount >= 3);

        viewModel.DraftProductId = string.Empty;

        Assert.False(viewModel.AddDraftItemCommand.CanExecute(null));
    }

    [Fact]
    public void AvaloniaComposition_UsesCashierOperatorSeamForPurchaseAudit()
    {
        string appSource = File.ReadAllText(FindRepositoryFile("src/POS.Presentation.Avalonia/App.axaml.cs"));

        Assert.DoesNotContain("LocalPrototypeOperatorId", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", appSource, StringComparison.Ordinal);
        Assert.Contains("CashierUserId", appSource, StringComparison.Ordinal);
        Assert.Contains("() => cashier.CashierUserId", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainShell_ExposesPurchasesModuleAsImplementedWithoutActivatingReceivablesOrReports()
    {
        var shell = new MainShellViewModel { Purchases = CreateViewModel(new StaticSupplierLookupQueryService([])) };
        var suppliers = shell.Modules.Single(module => module.Key == "suppliers");
        var reports = shell.Modules.Single(module => module.Key == "reports");

        shell.SelectedModule = suppliers;

        Assert.True(suppliers.IsImplemented);
        Assert.True(shell.IsPurchasesSelected);
        Assert.False(reports.IsImplemented);
        Assert.False(shell.IsPlannedModuleSelected);
    }

    private static PurchasesViewModel CreateViewModel(
        ISupplierLookupQueryService supplierLookup,
        InMemorySupplierRepository? repository = null,
        StaticRegisterPurchaseUseCase? purchaseUseCase = null,
        Func<Guid>? currentOperatorIdProvider = null)
    {
        repository ??= new InMemorySupplierRepository();
        return new PurchasesViewModel(
            new SaveSupplierUseCase(repository, new InMemoryUnitOfWork()),
            new SearchSuppliersUseCase(supplierLookup),
            purchaseUseCase ?? new StaticRegisterPurchaseUseCase(new RegisterPurchaseResult(Guid.NewGuid(), Guid.NewGuid(), 0m, 0)),
            currentOperatorIdProvider ?? (() => Guid.NewGuid()));
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
