using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.BarcodeLabels;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Reporting;
using POS.Application.UseCases.Sales;
using POS.Application.UseCases.Settings;
using POS.Application.UseCases.Shifts;
using POS.Domain.Enums;
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

            if (!ShowLoginAndLaunchShell())
                Shutdown(0);
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

    // Returns true if user logged in and shell was shown; false if user closed login
    private bool ShowLoginAndLaunchShell()
    {
        // Prevent app shutdown when login window closes (no windows open momentarily)
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var loginUseCase = _scope!.ServiceProvider.GetRequiredService<LoginUseCase>();
        var writableSession = _scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>();
        var loginVm = new ViewModels.LoginViewModel(loginUseCase, writableSession);
        var loginWindow = new Views.LoginWindow(loginVm);

        if (loginWindow.ShowDialog() != true)
            return false;

        MainShellViewModel shellVm;
        try
        {
            shellVm = BuildMainShellViewModel(_scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error al construir el shell principal:\n\n{ex}",
                "Error de inicio");
            return false;
        }

        var shell = new Views.NavigationShellView
        {
            DataContext = shellVm
        };

        shellVm.LogoutRequested += () =>
        {
            // Clear session and close current shell
            _scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>().ClearSession();
            shell.Close();

            // Re-enter login flow
            if (!ShowLoginAndLaunchShell())
                Shutdown(0);
        };

        MainWindow = shell;
        shell.Show();

        // Now that shell is open, restore normal shutdown behavior
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        return true;
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
        var session = sp.GetRequiredService<ICurrentOperatorSession>();
        var role = session.Role;
        var username = session.Username;

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
            RedeemLoyaltyPointsUseCase? redeemLoyalty = null;
            try { redeemLoyalty = sp.GetRequiredService<RedeemLoyaltyPointsUseCase>(); } catch { }
            cashier = new CashierViewModel(productLookup, new PreviewPaymentUseCase(), createSale, redeemLoyalty);
        }
        catch
        {
            cashier = new CashierViewModel(productLookup);
        }

        cashier.CashierUserId = session.OperatorId;

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

        ReportsViewModel? reports = null;
        try
        {
            var getReport = sp.GetRequiredService<IGetOperationalReportUseCase>();
            IProfitQueryService? profitQuery = null;
            try { profitQuery = sp.GetRequiredService<IProfitQueryService>(); } catch { }
            IReportExportService? exportService = null;
            try { exportService = sp.GetRequiredService<IReportExportService>(); } catch { }
            CloseCashRegisterUseCase? closeCash = null;
            try { closeCash = sp.GetRequiredService<CloseCashRegisterUseCase>(); } catch { }
            ICurrentOperatorSession? operatorSession = null;
            try { operatorSession = sp.GetRequiredService<ICurrentOperatorSession>(); } catch { }
            reports = new ReportsViewModel(getReport, profitQuery, exportService, closeCash, operatorSession);
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
            var importProductsCsv = sp.GetRequiredService<ImportProductsCsvUseCase>();
            SearchCategoriesUseCase? searchCategories = null;
            try { searchCategories = sp.GetRequiredService<SearchCategoriesUseCase>(); } catch { }
            products = new ProductsViewModel(createProduct, updateProduct, deleteProduct, searchProducts, searchCategories, importProductsCsv);

            CategoriesViewModel? categoriesEditor = null;
            try
            {
                var searchCat = sp.GetRequiredService<SearchCategoriesUseCase>();
                var createCat = sp.GetRequiredService<CreateCategoryUseCase>();
                var updateCat = sp.GetRequiredService<UpdateCategoryUseCase>();
                categoriesEditor = new CategoriesViewModel(searchCat, createCat, updateCat);
            }
            catch { }

            if (categoriesEditor is not null)
            {
                products = new ProductsViewModel(createProduct, updateProduct, deleteProduct, searchProducts, searchCategories, importProductsCsv)
                {
                    CategoriesEditor = categoriesEditor
                };
            }
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

        LoyaltyViewModel? loyalty = null;
        try
        {
            var getBalance = sp.GetRequiredService<GetLoyaltyBalanceUseCase>();
            var redeemPoints = sp.GetRequiredService<RedeemLoyaltyPointsUseCase>();
            loyalty = new LoyaltyViewModel(getBalance, redeemPoints);
        }
        catch
        {
        }

        BarcodeViewModel? barcode = null;
        try
        {
            var generateBarcode = sp.GetRequiredService<GenerateBarcodeUseCase>();
            var printLabels = sp.GetRequiredService<PrintLabelsUseCase>();
            barcode = new BarcodeViewModel(generateBarcode, printLabels, productLookup);
        }
        catch
        {
        }

        UserManagementViewModel? userManagement = null;
        try
        {
            var getAllUsers = sp.GetRequiredService<GetAllUsersUseCase>();
            var createUser = sp.GetRequiredService<CreateUserUseCase>();
            var updateUser = sp.GetRequiredService<UpdateUserUseCase>();
            var deactivateUser = sp.GetRequiredService<DeactivateUserUseCase>();
            userManagement = new UserManagementViewModel(getAllUsers, createUser, updateUser, deactivateUser, session);
        }
        catch
        {
        }

        ShiftsViewModel? shifts = null;
        try
        {
            var startShift = sp.GetRequiredService<StartShiftUseCase>();
            var closeShift = sp.GetRequiredService<CloseShiftUseCase>();
            var getShifts = sp.GetRequiredService<GetShiftsUseCase>();
            shifts = new ShiftsViewModel(startShift, closeShift, getShifts, session);
        }
        catch
        {
        }

        AuditLogViewModel? auditLog = null;
        try
        {
            auditLog = new AuditLogViewModel(sp.GetRequiredService<GetAuditLogsUseCase>());
        }
        catch
        {
        }

        return new MainShellViewModel(cashier, dashboardUseCase, role, username)
        {
            Quote = quote,
            QuoteHistory = quoteHistory,
            Customers = customers,
            Purchases = purchases,
            Reports = reports,
            Products = products,
            Inventory = inventory,
            Invoices = invoices,
            Settings = settings,
            Loyalty = loyalty,
            Barcode = barcode,
            UserManagement = userManagement,
            Shifts = shifts,
            AuditLog = auditLog
        };
    }
}
