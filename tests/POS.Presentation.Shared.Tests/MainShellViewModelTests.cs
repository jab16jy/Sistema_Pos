using POS.Domain.Interfaces;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class MainShellViewModelTests
{
    private const int FuncbarModuleCount = 5;
    private const string CashierModuleKey = "cashier";
    private const string QuotesModuleKey = "quotes";

    [Fact]
    public void Constructor_LoadsActiveModuleContractAndSelectsCashier()
    {
        var viewModel = new MainShellViewModel();

        Assert.Equal(FuncbarModuleCount, viewModel.Modules.Count);
        Assert.Equal(viewModel.FuncbarModules, viewModel.Modules);
        Assert.Contains(viewModel.Modules, module => module.Key == QuotesModuleKey);
        Assert.DoesNotContain(viewModel.Modules, module => module.Key == "receivables");
        Assert.Equal(CashierModuleKey, viewModel.SelectedModule.Key);
        Assert.True(viewModel.IsCashierSelected);
        Assert.Equal("Cobrar", viewModel.MainActionText);
    }

    [Fact]
    public void Constructor_ExposesWireframeFuncbarAndAdminHubProjection()
    {
        var viewModel = new MainShellViewModel();

        Assert.Equal(["cashier", "customers", "quotes", "invoices", "admin"], viewModel.FuncbarModules.Select(module => module.Key));
        Assert.Equal(["F1", "F2", "F3", "F4", "F5"], viewModel.FuncbarModules.Select(module => module.Shortcut));
        Assert.Equal(["products", "inventory", "suppliers", "reports", "shifts", "loyalty", "backup"], viewModel.AdminHubModules.Select(module => module.Key));
        Assert.DoesNotContain(viewModel.AdminHubModules, module => module.Key == "receivables");
        Assert.DoesNotContain(viewModel.AdminHubModules, module => module.Key == "barcode");
    }

    [Fact]
    public void SelectingImplementedModule_ChangesMessageAndActionWithoutBusinessLogic()
    {
        var viewModel = new MainShellViewModel();
        var productsModule = viewModel.AdminHubModules.Single(module => module.Key == "products");

        viewModel.SelectedModule = productsModule;

        Assert.False(viewModel.IsCashierSelected);
        Assert.True(viewModel.IsProductsSelected);
        Assert.True(viewModel.IsAdminSectionSelected);
        Assert.Equal("admin", viewModel.CurrentFuncbarModule.Key);
        Assert.Equal("Abrir módulo", viewModel.MainActionText);
        Assert.Contains("Productos", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectModuleCommand_UpdatesSelectedModule()
    {
        var viewModel = new MainShellViewModel();
        ModuleNavigationItem reportsModule = viewModel.AdminHubModules.Single(module => module.Key == "reports");

        viewModel.SelectModuleCommand.Execute(reportsModule);

        Assert.Equal(reportsModule, viewModel.SelectedModule);
    }

    [Fact]
    public void SelectingAdminHub_UsesAdminFuncbarEntry()
    {
        var viewModel = new MainShellViewModel();
        ModuleNavigationItem adminModule = viewModel.FuncbarModules.Single(module => module.Key == "admin");

        viewModel.SelectModuleCommand.Execute(adminModule);

        Assert.True(viewModel.IsAdminHubSelected);
        Assert.True(viewModel.IsAdminSectionSelected);
        Assert.Equal(adminModule, viewModel.CurrentFuncbarModule);
    }

    [Fact]
    public void SelectingAdminSubmodule_SetsIsAdminSubmoduleSelectedToTrue()
    {
        var viewModel = new MainShellViewModel();
        var productsModule = viewModel.AdminHubModules.Single(module => module.Key == "products");

        viewModel.SelectedModule = productsModule;

        Assert.True(viewModel.IsAdminSubmoduleSelected);
    }

    [Fact]
    public void GoBackToAdminCommand_NavigatesToAdminHub()
    {
        var viewModel = new MainShellViewModel();
        var productsModule = viewModel.AdminHubModules.Single(module => module.Key == "products");
        viewModel.SelectedModule = productsModule;

        Assert.True(viewModel.IsAdminSubmoduleSelected);

        viewModel.GoBackToAdminCommand.Execute(null);

        Assert.True(viewModel.IsAdminHubSelected);
        Assert.False(viewModel.IsAdminSubmoduleSelected);
    }

    [Fact]
    public void CashierPermissions_RestrictVisibleSurfaceToOperationalModulesAndEmptyAdminHub()
    {
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            null,
            POS.Domain.Authorization.PermissionKeys.CashierDefaults,
            POS.Domain.Authorization.RoleConstants.CashierRoleName,
            "cajero1");

        Assert.Equal(["cashier", "customers", "quotes", "invoices", "shifts"], viewModel.FuncbarModules.Select(module => module.Key));
        Assert.Equal(["F1", "F2", "F3", "F4", "F5"], viewModel.FuncbarModules.Select(module => module.Shortcut));
        Assert.Empty(viewModel.AdminHubModules);
        Assert.Empty(viewModel.AdminHubExtraModules);
        Assert.DoesNotContain(viewModel.FuncbarModules, module => module.Key is "admin" or "users" or "audit" or "products" or "reports");
    }

    [Fact]
    public void CashierPermissions_SelectingTurnos_ResolvesFuncbarWithoutThrowing()
    {
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            null,
            POS.Domain.Authorization.PermissionKeys.CashierDefaults,
            POS.Domain.Authorization.RoleConstants.CashierRoleName,
            "cajero1");

        viewModel.SelectedModule = viewModel.FuncbarModules.Single(module => module.Key == "shifts");

        Assert.True(viewModel.IsShiftsSelected);
        Assert.Equal("shifts", viewModel.CurrentFuncbarModule.Key);
    }

    [Fact]
    public void CustomRoleWithAdminViewPermission_KeepsOperationalFuncbarAndHidesUnauthorizedShift()
    {
        var permissions = new HashSet<string>
        {
            POS.Domain.Authorization.PermissionKeys.Products.View
        };
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            null,
            permissions,
            "Custom",
            "operator");

        Assert.Equal(["cashier", "customers", "quotes", "invoices", "admin"], viewModel.FuncbarModules.Select(module => module.Key));
        Assert.Equal(["products"], viewModel.AdminHubModules.Select(module => module.Key));
        Assert.Empty(viewModel.AdminHubExtraModules);
        Assert.DoesNotContain(viewModel.FuncbarModules, module => module.Key == "shifts");
        Assert.DoesNotContain(viewModel.AdminHubModules, module => module.Key == "shifts");
    }

    [Fact]
    public async Task LoadDashboardMetricsAsync_UsesPersistedSalesSummary()
    {
        var repository = new StaticSaleSummaryRepository(new DashboardSummaryData(
            125_000m,
            75_000m,
            50_000m,
            3,
            4));
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            new GetDashboardSummaryUseCase(repository));

        await viewModel.LoadDashboardMetricsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Ventas del día" && metric.Value == "$125.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Efectivo en caja" && metric.Value == "$75.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Pagos digitales" && metric.Value == "$50.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Stock bajo" && metric.Value == "3");
        Assert.Contains("4 venta", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset expectedStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).ToUniversalTime();
        Assert.NotNull(repository.LastFrom);
        Assert.NotNull(repository.LastTo);
        Assert.Equal(expectedStart, repository.LastFrom.Value);
        Assert.Equal(expectedStart.AddDays(1), repository.LastTo.Value);
        Assert.Equal(TimeSpan.FromDays(1), repository.LastTo.Value - repository.LastFrom.Value);
    }

    [Fact]
    public async Task RefreshDashboardMetrics_LoadsPersistedSalesSummary()
    {
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            new GetDashboardSummaryUseCase(new StaticSaleSummaryRepository(new DashboardSummaryData(
                90_000m,
                30_000m,
                60_000m,
                2,
                3))));

        viewModel.RefreshDashboardMetrics();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Ventas del día" && metric.Value == "$90.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Efectivo en caja" && metric.Value == "$30.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Pagos digitales" && metric.Value == "$60.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Stock bajo" && metric.Value == "2");
        Assert.Contains("3 venta", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectingProductsModule_LoadsProductsAutomatically()
    {
        Category category = new("Sin categoría");
        var viewModel = new MainShellViewModel
        {
            Products = CreateProductsViewModel(
            [new ProductSearchResult(Guid.NewGuid(), "Aceite", category.Id, "Sin categoría", null, 25_000m, 3m, 1m, 19m, null, "ACE-1", true)],
            [category])
        };

        viewModel.SelectedModule = viewModel.AdminHubModules.Single(module => module.Key == "products");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(viewModel.Products!.Products);
    }

    [Fact]
    public async Task SelectingInventoryModule_LoadsStockAutomatically()
    {
        var viewModel = new MainShellViewModel
        {
            Inventory = CreateInventoryViewModel([
                new InventoryStockLevel(Guid.NewGuid(), "Aceite", "ACE-1", 3m, 1m, false)
            ])
        };

        viewModel.SelectedModule = viewModel.AdminHubModules.Single(module => module.Key == "inventory");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(viewModel.Inventory!.StockLevels);
    }

    private sealed class StaticSaleSummaryRepository : ISaleRepository
    {
        private readonly DashboardSummaryData _summary;
        public DateTimeOffset? LastFrom { get; private set; }
        public DateTimeOffset? LastTo { get; private set; }

        public StaticSaleSummaryRepository(DashboardSummaryData summary) => _summary = summary;

        public Task AddAsync(Domain.Entities.Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Domain.Entities.Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Domain.Entities.Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            LastFrom = from;
            LastTo = to;
            return Task.FromResult(_summary);
        }
    }

    private static ProductsViewModel CreateProductsViewModel(
        IReadOnlyList<ProductSearchResult> results,
        IReadOnlyList<Category> categories)
    {
        var productRepository = new StubProductRepository();
        var categoryRepository = new StubCategoryRepository(categories);

        return new ProductsViewModel(
            new CreateProductUseCase(productRepository, categoryRepository, new NoOpUnitOfWork()),
            new UpdateProductUseCase(productRepository, categoryRepository, new NoOpUnitOfWork()),
            new DeleteProductUseCase(productRepository, new NoOpUnitOfWork()),
            new SearchProductsUseCase(new StubProductCatalogQueryService(results)),
            new SearchCategoriesUseCase(categoryRepository));
    }

    private static InventoryViewModel CreateInventoryViewModel(IReadOnlyList<InventoryStockLevel> stockLevels)
    {
        var inventoryRepository = new StubInventoryRepository(stockLevels);
        var productRepository = new StubProductRepository();
        return new InventoryViewModel(
            new QueryStockUseCase(inventoryRepository),
            new QueryLowStockUseCase(inventoryRepository),
            new QueryMovementHistoryUseCase(inventoryRepository),
            new AdjustStockUseCase(productRepository, inventoryRepository, new NoOpUnitOfWork()));
    }

    private sealed class StubProductCatalogQueryService : IProductCatalogQueryService
    {
        private readonly IReadOnlyList<ProductSearchResult> _results;

        public StubProductCatalogQueryService(IReadOnlyList<ProductSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_results);
    }

    private sealed class StubCategoryRepository : ICategoryRepository
    {
        private readonly IReadOnlyList<Category> _categories;

        public StubCategoryRepository(IReadOnlyList<Category> categories)
        {
            _categories = categories;
        }

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_categories);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_categories.SingleOrDefault(category => category.Id == id));

        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubInventoryRepository : IInventoryRepository
    {
        private readonly IReadOnlyList<InventoryStockLevel> _stockLevels;

        public StubInventoryRepository(IReadOnlyList<InventoryStockLevel> stockLevels)
        {
            _stockLevels = stockLevels;
        }

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_stockLevels);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
