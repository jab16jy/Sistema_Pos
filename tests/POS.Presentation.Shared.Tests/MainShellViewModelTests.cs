using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class MainShellViewModelTests
{
    private const int ActiveShellModuleCount = 9;
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
    public void SelectingPlannedModule_ChangesMessageAndActionWithoutBusinessLogic()
    {
        var viewModel = new MainShellViewModel();
        var productsModule = viewModel.Modules.Single(module => module.Key == "products");

        viewModel.SelectedModule = productsModule;

        Assert.False(viewModel.IsCashierSelected);
        Assert.Equal("Abrir módulo", viewModel.MainActionText);
        Assert.Contains("planificado", viewModel.OperatorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectModuleCommand_UpdatesSelectedModule()
    {
        var viewModel = new MainShellViewModel();
        ModuleNavigationItem reportsModule = viewModel.Modules.Single(module => module.Key == "reports");

        viewModel.SelectModuleCommand.Execute(reportsModule);

        Assert.Equal(reportsModule, viewModel.SelectedModule);
    }
}
