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
        Assert.Equal(["products", "inventory", "suppliers", "reports", "shifts", "loyalty", "barcode"], viewModel.AdminHubModules.Select(module => module.Key));
        Assert.DoesNotContain(viewModel.AdminHubModules, module => module.Key == "receivables");
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
    public async Task LoadDashboardMetricsAsync_UsesPersistedSalesSummary()
    {
        var viewModel = new MainShellViewModel(
            new CashierViewModel(new PrototypeProductLookupService()),
            new GetDashboardSummaryUseCase(new StaticSaleSummaryRepository(new DashboardSummaryData(
                125_000m,
                75_000m,
                50_000m,
                3,
                4))));

        await viewModel.LoadDashboardMetricsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Ventas del día" && metric.Value == "$125.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Efectivo en caja" && metric.Value == "$75.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Pagos digitales" && metric.Value == "$50.000");
        Assert.Contains(viewModel.Metrics, metric => metric.Label == "Stock bajo" && metric.Value == "3");
        Assert.Contains("4 venta", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
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

        public StaticSaleSummaryRepository(DashboardSummaryData summary) => _summary = summary;

        public Task AddAsync(Domain.Entities.Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Domain.Entities.Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Domain.Entities.Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(_summary);
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
