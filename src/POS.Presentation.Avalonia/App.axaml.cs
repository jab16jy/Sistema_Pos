using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Sales;
using POS.Infrastructure;
using POS.Presentation.Avalonia.Views;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;

// WU3: receivables module imports

namespace POS.Presentation.Avalonia;

public partial class App : global::Avalonia.Application
{
    private ServiceProvider? _services;
    private IServiceScope? _scope;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServiceProvider();
        DatabaseSchemaRollout.EnsureLatestSchemaAsync(_services).GetAwaiter().GetResult();
        _scope = _services.CreateScope();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = BuildMainShellViewModel(_scope.ServiceProvider),
            };

            desktop.Exit += (_, _) =>
            {
                _scope.Dispose();
                _services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Infrastructure registers repositories, use cases, and services backed by SQLite
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

        // Cashier always falls back to prototype product lookup because a real IProductLookupService
        // (backed by the database) is not yet registered. The sale persistence use case is real.
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
            // Customers stays null; MainWindow shows a safe empty-state fallback.
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
            // Purchases stays null; MainWindow shows a safe empty-state fallback.
        }

        // Quote history resolves from DI — real repositories, real use cases
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
            // QuoteHistory stays null; MainWindow shows the planned module placeholder
        }

        // Dashboard summary resolves from DI — real persisted sales metrics
        GetDashboardSummaryUseCase? dashboardUseCase = null;
        try
        {
            dashboardUseCase = sp.GetRequiredService<GetDashboardSummaryUseCase>();
        }
        catch
        {
            // Dashboard stays at prototype zero values
        }

        // Receivables ViewModel: resolves IReceivableQueryService and IApplyReceivablePaymentUseCase from DI
        ReceivablesViewModel? receivables = null;
        try
        {
            var receivableQueryService = sp.GetRequiredService<IReceivableQueryService>();
            var applyPaymentUseCase = sp.GetRequiredService<IApplyReceivablePaymentUseCase>();
            receivables = new ReceivablesViewModel(receivableQueryService, applyPaymentUseCase);
        }
        catch
        {
            // Receivables stays null; MainWindow shows a safe empty-state fallback.
        }

        return new MainShellViewModel(cashier, dashboardUseCase)
        {
            QuoteHistory = quoteHistory,
            Customers = customers,
            Purchases = purchases,
            Receivables = receivables
        };
    }
}
