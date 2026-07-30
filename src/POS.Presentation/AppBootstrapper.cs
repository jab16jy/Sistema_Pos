using System.IO;
using System.Text;
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
using POS.Application.UseCases.Roles;
using POS.Application.UseCases.Sales;
using POS.Application.UseCases.Settings;
using POS.Application.UseCases.Shifts;
using POS.Infrastructure;
using POS.Infrastructure.Services;
using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation;

internal static class AppBootstrapper
{
    internal static ServiceProvider BuildServiceProvider(string? connectionString = null)
    {
        var services = new ServiceCollection();
        services.AddPosInfrastructure(connectionString ?? BuildDefaultConnectionString(), ConfigureRuntimeOptions);
        return services.BuildServiceProvider();
    }

    internal static Task EnsureLatestSchemaAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        => DatabaseSchemaRollout.EnsureLatestSchemaAsync(serviceProvider, cancellationToken);

    internal static MainShellViewModel BuildMainShellViewModel(IServiceProvider sp)
    {
        var session = sp.GetRequiredService<ICurrentOperatorSession>();
        var roleName = session.RoleName;
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
                TryResolveOptional<ReprintReceiptUseCase>(sp),
                TryResolveOptional<SearchCustomersUseCase>(sp),
                 TryResolveOptional<GetCustomerPayableQuotesUseCase>(sp),
                 TryResolveOptional<ConvertQuoteToSaleUseCase>(sp),
                 sp.GetRequiredService<FindProductByBarcodeUseCase>(),
                 TryResolveOptional<GetLoyaltyBalanceUseCase>(sp),
                 sp.GetRequiredService<HoldSaleUseCase>(),
                 sp.GetRequiredService<GetHeldSalesUseCase>(),
                 sp.GetRequiredService<ResumeHeldSaleUseCase>(),
                 sp.GetRequiredService<CompleteHeldSaleUseCase>(),
                 sp.GetRequiredService<CancelHeldSaleUseCase>()),
            () => new CashierViewModel(productLookup),
            moduleWarnings);

        cashier.CashierUserId = session.OperatorId;

        CustomersViewModel? customers = TryCreateOptionalModule(
            "Clientes",
            () => new CustomersViewModel(
                sp.GetRequiredService<SaveCustomerUseCase>(),
                sp.GetRequiredService<GetCustomerHistoryUseCase>(),
                sp.GetRequiredService<SearchCustomersUseCase>(),
                TryResolveOptional<GetLoyaltyBalanceUseCase>(sp),
                TryResolveOptional<IReceivableQueryService>(sp),
                TryResolveOptional<IApplyReceivablePaymentUseCase>(sp),
                session,
                sp.GetRequiredService<POS.Domain.Interfaces.ICashierShiftRepository>(),
                TryResolveOptional<GetLoyaltyBenefitsUseCase>(sp),
                TryResolveOptional<ClaimLoyaltyBenefitUseCase>(sp),
                TryResolveOptional<DeliverLoyaltyBenefitClaimUseCase>(sp),
                TryResolveOptional<CancelLoyaltyBenefitClaimUseCase>(sp)),
            moduleWarnings);

        PurchasesViewModel? purchases = TryCreateOptionalModule(
            "Compras",
            () => new PurchasesViewModel(
                sp.GetRequiredService<SaveSupplierUseCase>(),
                sp.GetRequiredService<SearchSuppliersUseCase>(),
                sp.GetRequiredService<RegisterPurchaseUseCase>(),
                () => cashier.CashierUserId,
                productLookup,
                sp.GetRequiredService<GetSupplierCatalogUseCase>(),
                sp.GetRequiredService<GetEligibleSupplierCatalogUseCase>(),
                sp.GetRequiredService<SaveSupplierCatalogItemUseCase>(),
                sp.GetRequiredService<SearchPurchaseHistoryUseCase>(),
                sp.GetRequiredService<DeleteSupplierUseCase>(),
                sp.GetRequiredService<DeleteSupplierCatalogItemUseCase>()),
            moduleWarnings);

        QuoteHistoryViewModel? quoteHistory = TryCreateOptionalModule(
            "Historial de cotizaciones",
            () => new QuoteHistoryViewModel(
                sp.GetRequiredService<GetQuoteHistoryUseCase>(),
                sp.GetRequiredService<ConvertQuoteToSaleUseCase>(),
                sp.GetRequiredService<POS.Application.UseCases.Communication.SendDocumentUseCase>(),
                session,
                TryResolveOptional<POS.Application.Interfaces.IPdfService>(sp),
                TryResolveOptional<GetQuoteDetailUseCase>(sp),
                TryResolveOptional<CancelQuoteUseCase>(sp)),
            moduleWarnings);

        QuoteViewModel? quote = TryCreateOptionalModule(
            "Cotizaciones",
            () => new QuoteViewModel(
                productLookup,
                sp.GetRequiredService<CreateQuoteUseCase>(),
                TryResolveOptional<POS.Application.UseCases.Communication.SendDocumentUseCase>(sp),
                TryResolveOptional<SearchCustomersUseCase>(sp)),
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
                AdjustStockUseCase? adjustStock = TryResolveOptional<AdjustStockUseCase>(sp);

                CategoriesViewModel? categoriesEditor = TryCreateOptionalModule(
                    "Editor de categorías",
                    () => new CategoriesViewModel(
                        sp.GetRequiredService<SearchCategoriesUseCase>(),
                        sp.GetRequiredService<CreateCategoryUseCase>(),
                        sp.GetRequiredService<UpdateCategoryUseCase>(),
                        sp.GetRequiredService<DeleteCategoryUseCase>(),
                        sp.GetRequiredService<SearchProductsUseCase>(),
                        sp.GetRequiredService<AssignProductsToCategoryUseCase>()),
                    moduleWarnings);

                if (categoriesEditor is not null)
                {
                    return new ProductsViewModel(
                        createProduct,
                        updateProduct,
                        deleteProduct,
                        searchProducts,
                        searchCategories,
                        importProductsCsv,
                        adjustStock,
                        sp.GetRequiredService<AssignInternalBarcodeUseCase>(),
                        sp.GetRequiredService<PrintLabelsUseCase>(),
                        sp.GetRequiredService<GetProductSupplierSummaryUseCase>())
                    {
                        CategoriesEditor = categoriesEditor,
                        OperatorUserId = cashier.CashierUserId
                    };
                }

                return new ProductsViewModel(
                    createProduct,
                    updateProduct,
                    deleteProduct,
                    searchProducts,
                    searchCategories,
                    importProductsCsv,
                    adjustStock,
                    sp.GetRequiredService<AssignInternalBarcodeUseCase>(),
                    sp.GetRequiredService<PrintLabelsUseCase>(),
                    sp.GetRequiredService<GetProductSupplierSummaryUseCase>())
                {
                    OperatorUserId = cashier.CashierUserId
                };
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
                session,
                TryResolveOptional<POS.Application.UseCases.Communication.SendDocumentUseCase>(sp),
                TryResolveOptional<POS.Application.Interfaces.IPdfService>(sp),
                TryResolveOptional<POS.Application.UseCases.Invoices.CancelSaleUseCase>(sp),
                TryResolveOptional<GetInvoiceDetailUseCase>(sp))
            { OperatorUserId = cashier.CashierUserId },
            moduleWarnings);

        SettingsViewModel? settings = TryCreateOptionalModule(
            "Configuración",
            () => new SettingsViewModel(
                sp.GetRequiredService<LoadSettingsUseCase>(),
                sp.GetRequiredService<SaveSettingsUseCase>(),
                TryResolveOptional<POS.Application.Interfaces.IEmailService>(sp),
                TryResolveOptional<POS.Application.Interfaces.IWhatsAppService>(sp),
                TryResolveOptional<POS.Application.Interfaces.IInstalledPrinterProvider>(sp),
                session,
                TryResolveOptional<POS.Domain.Interfaces.IUserRepository>(sp),
                TryResolveOptional<POS.Domain.Interfaces.ICashierShiftRepository>(sp)),
            moduleWarnings);

        BackupViewModel? backup = TryCreateOptionalModule(
            "Respaldos",
            () => new BackupViewModel(
                sp.GetRequiredService<LoadSettingsUseCase>(),
                sp.GetRequiredService<TriggerManualBackupUseCase>(),
                TryResolveOptional<UpdateBackupDirectoryUseCase>(sp),
                TryResolveOptional<RestoreBackupUseCase>(sp)),
            moduleWarnings);

        // Null when the catalogue use cases cannot be resolved: the Loyalty module then hides the
        // Beneficios tab instead of opening an empty pane.
        GetLoyaltyBenefitsUseCase? getBenefits = TryResolveOptional<GetLoyaltyBenefitsUseCase>(sp);
        LoyaltyBenefitsViewModel? loyaltyBenefits = getBenefits is null
            ? null
            : TryCreateOptionalModule(
                "Beneficios de puntos",
                () => new LoyaltyBenefitsViewModel(
                    getBenefits,
                    TryResolveOptional<SaveLoyaltyBenefitUseCase>(sp),
                    TryResolveOptional<DeleteLoyaltyBenefitUseCase>(sp),
                    TryResolveOptional<GetLoyaltyTiersUseCase>(sp)),
                moduleWarnings);

        LoyaltyViewModel? loyalty = TryCreateOptionalModule(
            "Puntos",
            () => new LoyaltyViewModel(
                sp.GetRequiredService<GetLoyaltyBalanceUseCase>(),
                sp.GetRequiredService<RedeemLoyaltyPointsUseCase>(),
                TryResolveOptional<GetLoyaltyProgramOverviewUseCase>(sp),
                TryResolveOptional<SetLoyaltyRuleUseCase>(sp),
                TryResolveOptional<GetLoyaltyTiersUseCase>(sp),
                TryResolveOptional<CreateLoyaltyTierUseCase>(sp),
                TryResolveOptional<UpdateLoyaltyTierUseCase>(sp),
                TryResolveOptional<DeleteLoyaltyTierUseCase>(sp),
                TryResolveOptional<GetLoyaltyTierLimitsUseCase>(sp),
                loyaltyBenefits),
            moduleWarnings);

        UserManagementViewModel? userManagement = TryCreateOptionalModule(
            "Usuarios",
            () => new UserManagementViewModel(
                sp.GetRequiredService<GetAllUsersUseCase>(),
                sp.GetRequiredService<CreateUserUseCase>(),
                sp.GetRequiredService<UpdateUserUseCase>(),
                sp.GetRequiredService<DeactivateUserUseCase>(),
                sp.GetRequiredService<GetAllRolesUseCase>(),
                session),
            moduleWarnings);

        RolesManagementViewModel? rolesManagement = TryCreateOptionalModule(
            "Roles",
            () => new RolesManagementViewModel(
                sp.GetRequiredService<GetAllRolesUseCase>(),
                sp.GetRequiredService<CreateRoleUseCase>(),
                sp.GetRequiredService<UpdateRoleUseCase>(),
                sp.GetRequiredService<DeactivateRoleUseCase>(),
                sp.GetRequiredService<DeleteRoleUseCase>(),
                sp.GetRequiredService<POS.Domain.Interfaces.IRoleRepository>(),
                session),
            moduleWarnings);

        ShiftsViewModel? shifts = TryCreateOptionalModule(
            "Turnos",
            () => new ShiftsViewModel(
                sp.GetRequiredService<StartShiftUseCase>(),
                sp.GetRequiredService<CloseShiftUseCase>(),
                sp.GetRequiredService<GetShiftsUseCase>(),
                sp.GetRequiredService<GetShiftSalesSummaryUseCase>(),
                session,
                sp.GetRequiredService<POS.Domain.Interfaces.IUserRepository>()),
            moduleWarnings);

        AuditLogViewModel? auditLog = TryCreateOptionalModule(
            "Auditoría",
            () => new AuditLogViewModel(sp.GetRequiredService<GetAuditLogsUseCase>(), session),
            moduleWarnings);

        var shellViewModel = new MainShellViewModel(cashier, dashboardUseCase, session.Permissions, roleName, username)
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
            Backup = backup,
            Loyalty = loyalty,
            UserManagement = userManagement,
            RolesManagement = rolesManagement,
            Shifts = shifts,
            AuditLog = auditLog
        };

        if (moduleWarnings.Count > 0)
        {
            shellViewModel.ShowOperatorWarning(BuildModuleWarningMessage(moduleWarnings));
        }

        shellViewModel.ConnectCustomerInvoiceNavigation();
        shellViewModel.ConnectReportsShiftNavigation();

        return shellViewModel;
    }

    private static string BuildDefaultConnectionString()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PosLocal",
            "pos.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        return $"Data Source={dbPath}";
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
        options.Printer.Port = Environment.GetEnvironmentVariable("POS_PRINTER_PORT");
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
