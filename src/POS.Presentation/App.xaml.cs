using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Application.UseCases.Settings;
using POS.Infrastructure;
using POS.Infrastructure.Services;
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

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error de inicio");
            ex.Handled = true;
            Shutdown(1);
        };

        try
        {
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
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Error de inicio");
            Shutdown(1);
        }
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
        services.AddPosInfrastructure($"Data Source={dbPath}", ConfigureRuntimeOptions);
        return services.BuildServiceProvider();
    }

    private static void ConfigureRuntimeOptions(PosRuntimeOptions options)
    {
        options.Smtp.Host = Environment.GetEnvironmentVariable("POS_SMTP_HOST");
        options.Smtp.FromAddress = Environment.GetEnvironmentVariable("POS_SMTP_FROM");
        options.Smtp.User = Environment.GetEnvironmentVariable("POS_SMTP_USER");
        options.Smtp.Password = Environment.GetEnvironmentVariable("POS_SMTP_PASSWORD");
        if (int.TryParse(Environment.GetEnvironmentVariable("POS_SMTP_PORT"), out int smtpPort))
        {
            options.Smtp.Port = smtpPort;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("POS_SMTP_SSL"), out bool smtpSsl))
        {
            options.Smtp.UseSsl = smtpSsl;
        }

        options.WhatsAppScript.FileName = Environment.GetEnvironmentVariable("POS_WHATSAPP_SCRIPT");
        options.WhatsAppScript.WorkingDirectory = Environment.GetEnvironmentVariable("POS_WHATSAPP_WORKDIR");
        options.Printer.Command = Environment.GetEnvironmentVariable("POS_PRINTER_COMMAND");
        options.Printer.WorkingDirectory = Environment.GetEnvironmentVariable("POS_PRINTER_WORKDIR");
    }

    private static MainShellViewModel BuildMainShellViewModel(IServiceProvider sp)
    {
        IProductLookupService productLookup;
        try
        {
            productLookup = sp.GetRequiredService<IProductLookupService>();
        }
        catch
        {
            productLookup = new PrototypeProductLookupService();
        }

        CashierViewModel cashier;
        try
        {
            var createSale = sp.GetRequiredService<CreateSaleUseCase>();
            cashier = new CashierViewModel(productLookup, createSale);
        }
        catch
        {
            cashier = new CashierViewModel(productLookup);
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
            quote = new QuoteViewModel(productLookup, createQuote);
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

        ProductsViewModel? products = null;
        try
        {
            var createProduct = sp.GetRequiredService<CreateProductUseCase>();
            var updateProduct = sp.GetRequiredService<UpdateProductUseCase>();
            var deleteProduct = sp.GetRequiredService<DeleteProductUseCase>();
            var searchProducts = sp.GetRequiredService<SearchProductsUseCase>();
            products = new ProductsViewModel(createProduct, updateProduct, deleteProduct, searchProducts);
        }
        catch
        {
        }

        InventoryViewModel? inventory = null;
        try
        {
            var queryStock = sp.GetRequiredService<QueryStockUseCase>();
            var queryLowStock = sp.GetRequiredService<QueryLowStockUseCase>();
            var queryMovementHistory = sp.GetRequiredService<QueryMovementHistoryUseCase>();
            var adjustStock = sp.GetRequiredService<AdjustStockUseCase>();
            inventory = new InventoryViewModel(queryStock, queryLowStock, queryMovementHistory, adjustStock)
            {
                OperatorUserId = cashier.CashierUserId
            };
        }
        catch
        {
        }

        InvoicesViewModel? invoices = null;
        try
        {
            var querySalesHistory = sp.GetRequiredService<QuerySalesHistoryUseCase>();
            var reprintReceipt = sp.GetRequiredService<ReprintReceiptUseCase>();
            var processReturn = sp.GetRequiredService<ProcessReturnUseCase>();
            invoices = new InvoicesViewModel(querySalesHistory, reprintReceipt, processReturn)
            {
                OperatorUserId = cashier.CashierUserId
            };
        }
        catch
        {
        }

        SettingsViewModel? settings = null;
        try
        {
            var loadSettings = sp.GetRequiredService<LoadSettingsUseCase>();
            var saveSettings = sp.GetRequiredService<SaveSettingsUseCase>();
            var triggerBackup = sp.GetRequiredService<TriggerManualBackupUseCase>();
            settings = new SettingsViewModel(loadSettings, saveSettings, triggerBackup);
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
            Reports = reports,
            Products = products,
            Inventory = inventory,
            Invoices = invoices,
            Settings = settings
        };
    }
}
