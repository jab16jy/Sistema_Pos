using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Infrastructure;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private IServiceScope? _scope;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServiceProvider();
        DatabaseSchemaRollout.EnsureLatestSchemaAsync(_services).GetAwaiter().GetResult();
        _scope = _services.CreateScope();

        var shell = new Views.NavigationShellView
        {
            DataContext = BuildMainShellViewModel(_scope.ServiceProvider)
        };

        MainWindow = shell;
        shell.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scope?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var dbPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "PosLocal",
            "pos.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);
        services.AddPosInfrastructure($"Data Source={dbPath}");
        return services.BuildServiceProvider();
    }

    private static MainShellViewModel BuildMainShellViewModel(IServiceProvider sp)
    {
        CashierViewModel cashier;
        try
        {
            var createSale = sp.GetRequiredService<CreateSaleUseCase>();
            cashier = new CashierViewModel(new PrototypeProductLookupService(), createSale);
        }
        catch
        {
            cashier = new CashierViewModel(new PrototypeProductLookupService());
        }

        cashier.CashierUserId = sp.GetRequiredService<ICurrentOperatorSession>().OperatorId;

        CustomersViewModel? customers = null;
        try
        {
            var saveCustomer = sp.GetRequiredService<SaveCustomerUseCase>();
            var getCustomerHistory = sp.GetRequiredService<GetCustomerHistoryUseCase>();
            var searchCustomers = sp.GetRequiredService<SearchCustomersUseCase>();
            customers = new CustomersViewModel(saveCustomer, getCustomerHistory, searchCustomers);
        }
        catch
        {
        }

        PurchasesViewModel? purchases = null;
        try
        {
            var saveSupplier = sp.GetRequiredService<SaveSupplierUseCase>();
            var searchSuppliers = sp.GetRequiredService<SearchSuppliersUseCase>();
            var registerPurchase = sp.GetRequiredService<RegisterPurchaseUseCase>();
            purchases = new PurchasesViewModel(saveSupplier, searchSuppliers, registerPurchase, () => cashier.CashierUserId);
        }
        catch
        {
        }

        QuoteHistoryViewModel? quoteHistory = null;
        try
        {
            var getHistory = sp.GetRequiredService<GetQuoteHistoryUseCase>();
            var convertQuote = sp.GetRequiredService<ConvertQuoteToSaleUseCase>();
            var sendDocument = sp.GetRequiredService<Application.UseCases.Communication.SendDocumentUseCase>();
            quoteHistory = new QuoteHistoryViewModel(getHistory, convertQuote, sendDocument);
        }
        catch
        {
        }

        QuoteViewModel? quote = null;
        try
        {
            var createQuote = sp.GetRequiredService<CreateQuoteUseCase>();
            quote = new QuoteViewModel(new PrototypeProductLookupService(), createQuote);
        }
        catch
        {
        }

        GetDashboardSummaryUseCase? dashboardUseCase = null;
        try
        {
            dashboardUseCase = sp.GetRequiredService<GetDashboardSummaryUseCase>();
        }
        catch
        {
        }

        ReceivablesViewModel? receivables = null;
        try
        {
            var receivableQueryService = sp.GetRequiredService<IReceivableQueryService>();
            var applyPaymentUseCase = sp.GetRequiredService<IApplyReceivablePaymentUseCase>();
            receivables = new ReceivablesViewModel(receivableQueryService, applyPaymentUseCase);
        }
        catch
        {
        }

        ReportsViewModel? reports = null;
        try
        {
            var getReport = sp.GetRequiredService<IGetOperationalReportUseCase>();
            reports = new ReportsViewModel(getReport);
        }
        catch
        {
        }

        return new MainShellViewModel(cashier, dashboardUseCase)
        {
            Quote = quote,
            QuoteHistory = quoteHistory,
            Customers = customers,
            Purchases = purchases,
            Receivables = receivables,
            Reports = reports
        };
    }
}
