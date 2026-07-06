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
using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;
using System.Text;

namespace POS.Presentation;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private IServiceScope? _scope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error de inicio");
            ex.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
            {
                System.Windows.MessageBox.Show(exception.ToString(), "Error no controlado");
            }

            Shutdown(1);
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error en tarea no observada");
            ex.SetObserved();
        };

        try
        {
            _services = BuildServiceProvider();
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(_services).ConfigureAwait(true);
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
            ShellLogoutCoordinator.Execute(
                () => ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
                () => _scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>().ClearSession(),
                shell.Close,
                ShowLoginAndLaunchShell,
                () => Shutdown(0));
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

        options.Printer.Command = Environment.GetEnvironmentVariable("POS_PRINTER_COMMAND");
        options.Printer.WorkingDirectory = Environment.GetEnvironmentVariable("POS_PRINTER_WORKDIR");
    }

    private static MainShellViewModel BuildMainShellViewModel(IServiceProvider sp)
    {
        var session = sp.GetRequiredService<ICurrentOperatorSession>();
        var role = session.Role;
        var username = session.Username;
        List<string> moduleWarnings = [];

        IProductLookupService productLookup = sp.GetRequiredService<IProductLookupService>();

        CashierViewModel cashier = TryCreateRequiredModule(
            "Caja",
            () => new CashierViewModel(
                productLookup,
                new PreviewPaymentUseCase(),
                sp.GetRequiredService<CreateSaleUseCase>(),
                TryResolveOptional<RedeemLoyaltyPointsUseCase>(sp),
                TryResolveOptional<ReprintReceiptUseCase>(sp)),
            () => new CashierViewModel(productLookup),
            moduleWarnings);

        cashier.CashierUserId = session.OperatorId;

        CustomersViewModel? customers = TryCreateOptionalModule(
            "Clientes",
            () => new CustomersViewModel(
                sp.GetRequiredService<SaveCustomerUseCase>(),
                sp.GetRequiredService<GetCustomerHistoryUseCase>(),
                sp.GetRequiredService<SearchCustomersUseCase>(),
                TryResolveOptional<GetLoyaltyBalanceUseCase>(sp)),
            moduleWarnings);

        PurchasesViewModel? purchases = TryCreateOptionalModule(
            "Compras",
            () => new PurchasesViewModel(
                sp.GetRequiredService<SaveSupplierUseCase>(),
                sp.GetRequiredService<SearchSuppliersUseCase>(),
                sp.GetRequiredService<RegisterPurchaseUseCase>(),
                () => cashier.CashierUserId),
            moduleWarnings);

        QuoteHistoryViewModel? quoteHistory = TryCreateOptionalModule(
            "Historial de cotizaciones",
            () => new QuoteHistoryViewModel(
                sp.GetRequiredService<GetQuoteHistoryUseCase>(),
                sp.GetRequiredService<ConvertQuoteToSaleUseCase>(),
                sp.GetRequiredService<Application.UseCases.Communication.SendDocumentUseCase>()),
            moduleWarnings);

        QuoteViewModel? quote = TryCreateOptionalModule(
            "Cotizaciones",
            () => new QuoteViewModel(
                productLookup,
                sp.GetRequiredService<CreateQuoteUseCase>(),
                TryResolveOptional<Application.UseCases.Communication.SendDocumentUseCase>(sp)),
            moduleWarnings);

        GetDashboardSummaryUseCase? dashboardUseCase = TryResolveOptional<GetDashboardSummaryUseCase>(sp);

        ReportsViewModel? reports = TryCreateOptionalModule(
            "Reportes",
            () => new ReportsViewModel(
                sp.GetRequiredService<IGetOperationalReportUseCase>(),
                TryResolveOptional<IProfitQueryService>(sp),
                TryResolveOptional<IReportExportService>(sp),
                TryResolveOptional<GetShiftsUseCase>(sp)),
            moduleWarnings);

        ProductsViewModel? products = TryCreateOptionalModule(
            "Productos",
            () =>
            {
                var createProduct = sp.GetRequiredService<CreateProductUseCase>();
                var updateProduct = sp.GetRequiredService<UpdateProductUseCase>();
                var deleteProduct = sp.GetRequiredService<DeleteProductUseCase>();
                var searchProducts = sp.GetRequiredService<SearchProductsUseCase>();
                var importProductsCsv = sp.GetRequiredService<ImportProductsCsvUseCase>();
                SearchCategoriesUseCase? searchCategories = TryResolveOptional<SearchCategoriesUseCase>(sp);

                CategoriesViewModel? categoriesEditor = TryCreateOptionalModule(
                    "Editor de categorías",
                    () => new CategoriesViewModel(
                        sp.GetRequiredService<SearchCategoriesUseCase>(),
                        sp.GetRequiredService<CreateCategoryUseCase>(),
                        sp.GetRequiredService<UpdateCategoryUseCase>()),
                    moduleWarnings);

                if (categoriesEditor is not null)
                {
                    return new ProductsViewModel(createProduct, updateProduct, deleteProduct, searchProducts, searchCategories, importProductsCsv)
                    {
                        CategoriesEditor = categoriesEditor
                    };
                }

                return new ProductsViewModel(createProduct, updateProduct, deleteProduct, searchProducts, searchCategories, importProductsCsv);
            },
            moduleWarnings);

        InventoryViewModel? inventory = TryCreateOptionalModule(
            "Inventario",
            () => new InventoryViewModel(
                sp.GetRequiredService<QueryStockUseCase>(),
                sp.GetRequiredService<QueryLowStockUseCase>(),
                sp.GetRequiredService<QueryMovementHistoryUseCase>(),
                sp.GetRequiredService<AdjustStockUseCase>())
            {
                OperatorUserId = cashier.CashierUserId
            },
            moduleWarnings);

        InvoicesViewModel? invoices = TryCreateOptionalModule(
            "Facturas",
            () => new InvoicesViewModel(
                sp.GetRequiredService<QuerySalesHistoryUseCase>(),
                sp.GetRequiredService<ReprintReceiptUseCase>(),
                sp.GetRequiredService<ProcessReturnUseCase>(),
                TryResolveOptional<Application.UseCases.Communication.SendDocumentUseCase>(sp),
                TryResolveOptional<Application.Interfaces.IPdfService>(sp),
                TryResolveOptional<Application.UseCases.Invoices.CancelSaleUseCase>(sp))
            {
                OperatorUserId = cashier.CashierUserId,
                CurrentUserRole = session.Role
            },
            moduleWarnings);

        SettingsViewModel? settings = TryCreateOptionalModule(
            "Configuración",
            () => new SettingsViewModel(
                sp.GetRequiredService<LoadSettingsUseCase>(),
                sp.GetRequiredService<SaveSettingsUseCase>(),
                sp.GetRequiredService<TriggerManualBackupUseCase>(),
                TryResolveOptional<Application.Interfaces.IEmailService>(sp),
                TryResolveOptional<Application.Interfaces.IWhatsAppService>(sp),
                TryResolveOptional<Application.Interfaces.IInstalledPrinterProvider>(sp)),
            moduleWarnings);

        LoyaltyViewModel? loyalty = TryCreateOptionalModule(
            "Puntos",
            () => new LoyaltyViewModel(
                sp.GetRequiredService<GetLoyaltyBalanceUseCase>(),
                sp.GetRequiredService<RedeemLoyaltyPointsUseCase>()),
            moduleWarnings);

        BarcodeViewModel? barcode = TryCreateOptionalModule(
            "Códigos de barras",
            () => new BarcodeViewModel(
                sp.GetRequiredService<GenerateBarcodeUseCase>(),
                sp.GetRequiredService<PrintLabelsUseCase>(),
                productLookup),
            moduleWarnings);

        UserManagementViewModel? userManagement = TryCreateOptionalModule(
            "Usuarios",
            () => new UserManagementViewModel(
                sp.GetRequiredService<GetAllUsersUseCase>(),
                sp.GetRequiredService<CreateUserUseCase>(),
                sp.GetRequiredService<UpdateUserUseCase>(),
                sp.GetRequiredService<DeactivateUserUseCase>(),
                session),
            moduleWarnings);

        ShiftsViewModel? shifts = TryCreateOptionalModule(
            "Turnos",
            () => new ShiftsViewModel(
                sp.GetRequiredService<StartShiftUseCase>(),
                sp.GetRequiredService<CloseShiftUseCase>(),
                sp.GetRequiredService<GetShiftsUseCase>(),
                session),
            moduleWarnings);

        AuditLogViewModel? auditLog = TryCreateOptionalModule(
            "Auditoría",
            () => new AuditLogViewModel(sp.GetRequiredService<GetAuditLogsUseCase>()),
            moduleWarnings);

        var shellViewModel = new MainShellViewModel(cashier, dashboardUseCase, role, username)
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

        if (moduleWarnings.Count > 0)
        {
            shellViewModel.ShowOperatorWarning(BuildModuleWarningMessage(moduleWarnings));
        }

        return shellViewModel;
    }

    private static TService? TryResolveOptional<TService>(IServiceProvider serviceProvider)
        where TService : class
        => serviceProvider.GetService<TService>();

    private static TModule? TryCreateOptionalModule<TModule>(
        string moduleName,
        Func<TModule> factory,
        ICollection<string> warnings)
        where TModule : class
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            warnings.Add($"{moduleName}: {ex.Message}");
            return null;
        }
    }

    private static TModule TryCreateRequiredModule<TModule>(
        string moduleName,
        Func<TModule> factory,
        Func<TModule> fallbackFactory,
        ICollection<string> warnings)
        where TModule : class
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            warnings.Add($"{moduleName}: {ex.Message}");
            return fallbackFactory();
        }
    }

    private static string BuildModuleWarningMessage(IReadOnlyCollection<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return string.Empty;
        }

        string modules = string.Join(", ", warnings.Select(warning => warning.Split(':', 2)[0]).Distinct().Take(4));
        string suffix = warnings.Count > 4 ? " y otros." : ".";

        var builder = new StringBuilder();
        builder.Append("Algunos módulos se iniciaron con capacidad reducida: ");
        builder.Append(modules);
        builder.Append(suffix);
        builder.Append(" Revisa dependencias opcionales o errores de inicialización.");
        return builder.ToString();
    }
}
