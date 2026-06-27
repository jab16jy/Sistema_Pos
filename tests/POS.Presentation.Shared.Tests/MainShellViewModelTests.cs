using POS.Domain.Interfaces;
using POS.Application.UseCases.Sales;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class MainShellViewModelTests
{
    private const int ActiveShellModuleCount = 10; // WU3: added receivables module
    private const string CashierModuleKey = "cashier";
    private const string QuotesModuleKey = "quotes";

    [Fact]
    public void Constructor_LoadsActiveModuleContractAndSelectsCashier()
    {
        var viewModel = new MainShellViewModel();

        Assert.Equal(ActiveShellModuleCount, viewModel.Modules.Count);
        Assert.Contains(viewModel.Modules, module => module.Key == QuotesModuleKey);
        Assert.Equal(CashierModuleKey, viewModel.SelectedModule.Key);
        Assert.True(viewModel.IsCashierSelected);
        Assert.Equal("Cobrar", viewModel.MainActionText);
    }

    [Fact]
    public void SelectingImplementedModule_ChangesMessageAndActionWithoutBusinessLogic()
    {
        var viewModel = new MainShellViewModel();
        var productsModule = viewModel.Modules.Single(module => module.Key == "products");

        viewModel.SelectedModule = productsModule;

        Assert.False(viewModel.IsCashierSelected);
        Assert.True(viewModel.IsProductsSelected);
        Assert.Equal("Abrir módulo", viewModel.MainActionText);
        Assert.Contains("Productos", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectModuleCommand_UpdatesSelectedModule()
    {
        var viewModel = new MainShellViewModel();
        ModuleNavigationItem reportsModule = viewModel.Modules.Single(module => module.Key == "reports");

        viewModel.SelectModuleCommand.Execute(reportsModule);

        Assert.Equal(reportsModule, viewModel.SelectedModule);
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
}
