using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.BarcodeLabels;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Receivables;
using POS.Application.UseCases.Reporting;
using POS.Application.UseCases.Sales;
using POS.Application.UseCases.Settings;
using POS.Application.UseCases.Shifts;
using POS.Domain.Interfaces;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPosInfrastructure(
        this IServiceCollection services,
        string connectionString,
        Action<PosRuntimeOptions>? configureRuntime = null)
    {
        var runtimeOptions = new PosRuntimeOptions();
        configureRuntime?.Invoke(runtimeOptions);

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<InMemoryUserSession>();
        services.AddSingleton<ICurrentOperatorSession>(sp => sp.GetRequiredService<InMemoryUserSession>());
        services.AddSingleton<IWritableOperatorSession>(sp => sp.GetRequiredService<InMemoryUserSession>());
        services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddSingleton(runtimeOptions.Smtp);
        services.AddSingleton(runtimeOptions.WhatsAppScript);
        services.AddSingleton(runtimeOptions.Printer);
        services.AddSingleton<IManualBackupService>(_ => new ManualBackupService(connectionString));
        services.AddScoped<IPrinterSettingsResolver, PrinterSettingsResolver>();
        services.AddScoped<IThermalPrinterTransport, ThermalPrinterTransport>();

        // Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISaleReturnRepository, SaleReturnRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IDocumentSendLogRepository, DocumentSendLogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IProductSupplierRepository, ProductSupplierRepository>();
        services.AddScoped<IReceivableRepository, ReceivableRepository>();
        services.AddScoped<ICashClosingRepository, CashClosingRepository>();
        services.AddScoped<ICashierShiftRepository, CashierShiftRepository>();
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Infrastructure services
        services.AddScoped<IPrintService, EscPosPrinterService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IExternalLinkLauncher, ExternalLinkLauncher>();
        services.AddScoped<IWhatsAppService, WhatsAppDesktopWebService>();
        services.AddScoped<IProductLookupService, ProductLookupService>();
        services.AddScoped<IProductCatalogQueryService, ProductCatalogQueryService>();
        services.AddScoped<ISalesHistoryQueryService, SalesHistoryQueryService>();
        services.AddScoped<IPrinterCommandValidator, PrinterCommandValidator>();
        services.AddSingleton<IInstalledPrinterProvider, InstalledPrinterProvider>();
        services.AddScoped<ICustomerHistoryQueryService, CustomerHistoryQueryService>();
        services.AddScoped<ICustomerLookupQueryService, CustomerLookupQueryService>();
        services.AddScoped<ISupplierLookupQueryService, SupplierLookupQueryService>();
        services.AddScoped<ISupplierCatalogQueryService, SupplierCatalogQueryService>();
        services.AddScoped<IPurchaseHistoryQueryService, PurchaseHistoryQueryService>();
        services.AddScoped<IReceivableQueryService, ReceivableLookupQueryService>();
        services.AddScoped<IOperationalReportQueryService, OperationalReportQueryService>();
        services.AddScoped<IProfitQueryService, ProfitQueryService>();
        services.AddScoped<IBarcodeGenerationService, ZXingBarcodeGenerationService>();
        services.AddScoped<ILabelPrintService, LabelPrintService>();

        // Application use cases
        services.AddScoped<SaveCustomerUseCase>();
        services.AddScoped<GetCustomerHistoryUseCase>();
        services.AddScoped<SearchCustomersUseCase>();
        services.AddScoped<CreateProductUseCase>();
        services.AddScoped<AssignInternalBarcodeUseCase>();
        services.AddScoped<FindProductByBarcodeUseCase>();
        services.AddScoped<ImportProductsCsvUseCase>();
        services.AddScoped<AssignProductsToCategoryUseCase>();
        services.AddScoped<UpdateProductUseCase>();
        services.AddScoped<DeleteProductUseCase>();
        services.AddScoped<SearchProductsUseCase>();
        services.AddScoped<QueryStockUseCase>();
        services.AddScoped<QueryLowStockUseCase>();
        services.AddScoped<QueryMovementHistoryUseCase>();
        services.AddScoped<AdjustStockUseCase>();
        services.AddScoped<QuerySalesHistoryUseCase>();
        services.AddScoped<GetInvoiceDetailUseCase>();
        services.AddScoped<ReprintReceiptUseCase>();
        services.AddScoped<ProcessReturnUseCase>();
        services.AddScoped<CancelSaleUseCase>();
        services.AddScoped<LoadSettingsUseCase>();
        services.AddScoped<SaveSettingsUseCase>();
        services.AddScoped<TriggerManualBackupUseCase>();
        services.AddScoped<RestoreBackupUseCase>();
        services.AddScoped<UpdateBackupDirectoryUseCase>();
        services.AddScoped<SaveSupplierUseCase>();
        services.AddScoped<SearchSuppliersUseCase>();
        services.AddScoped<RegisterPurchaseUseCase>();
        services.AddScoped<GetSupplierCatalogUseCase>();
        services.AddScoped<GetEligibleSupplierCatalogUseCase>();
        services.AddScoped<GetProductSupplierSummaryUseCase>();
        services.AddScoped<SaveSupplierCatalogItemUseCase>();
        services.AddScoped<DeleteSupplierUseCase>();
        services.AddScoped<DeleteSupplierCatalogItemUseCase>();
        services.AddScoped<SearchPurchaseHistoryUseCase>();
        services.AddScoped<CreateQuoteUseCase>();
        services.AddScoped<ConvertQuoteToSaleUseCase>();
        services.AddScoped<GetCustomerPayableQuotesUseCase>();
        services.AddScoped<GetQuoteHistoryUseCase>();
        services.AddScoped<GetQuoteDetailUseCase>();
        services.AddScoped<CreateSaleUseCase>();
        services.AddScoped<PreviewPaymentUseCase>();
        services.AddScoped<GetDashboardSummaryUseCase>();
        services.AddScoped<SendDocumentUseCase>();
        services.AddScoped<ResendDocumentUseCase>();
        services.AddScoped<IApplyReceivablePaymentUseCase, ApplyReceivablePaymentUseCase>();
        services.AddScoped<IGetOperationalReportUseCase, GetOperationalReportUseCase>();
        services.AddScoped<CloseCashRegisterUseCase>();
        services.AddScoped<StartShiftUseCase>();
        services.AddScoped<CloseShiftUseCase>();
        services.AddScoped<GetShiftsUseCase>();
        services.AddScoped<GetAuditLogsUseCase>();

        // User management use cases
        services.AddScoped<GetAllUsersUseCase>();
        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<UpdateUserUseCase>();
        services.AddScoped<DeactivateUserUseCase>();

        // New POS modules use cases
        services.AddScoped<AccumulateLoyaltyPointsUseCase>();
        services.AddScoped<RedeemLoyaltyPointsUseCase>();
        services.AddScoped<GetLoyaltyBalanceUseCase>();
        services.AddScoped<GetLoyaltyProgramOverviewUseCase>();
        services.AddScoped<SetLoyaltyRuleUseCase>();
        services.AddScoped<GetLoyaltyTiersUseCase>();
        services.AddScoped<CreateLoyaltyTierUseCase>();
        services.AddScoped<UpdateLoyaltyTierUseCase>();
        services.AddScoped<DeleteLoyaltyTierUseCase>();
        services.AddScoped<CreateCategoryUseCase>();
        services.AddScoped<UpdateCategoryUseCase>();
        services.AddScoped<SearchCategoriesUseCase>();
        services.AddScoped<DeleteCategoryUseCase>();
        services.AddScoped<GenerateBarcodeUseCase>();
        services.AddScoped<PrintLabelsUseCase>();

        return services;
    }
}
