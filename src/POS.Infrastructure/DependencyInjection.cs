using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Receivables;
using POS.Application.UseCases.Sales;
using POS.Domain.Interfaces;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPosInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<ICurrentOperatorSession, LocalFileOperatorSession>();

        // Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IDocumentSendLogRepository, DocumentSendLogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IReceivableRepository, ReceivableRepository>();

        // Infrastructure services
        services.AddScoped<IPrintService, PrintService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IWhatsAppService, WhatsAppScriptService>();
        services.AddScoped<ICustomerHistoryQueryService, CustomerHistoryQueryService>();
        services.AddScoped<ICustomerLookupQueryService, CustomerLookupQueryService>();
        services.AddScoped<ISupplierLookupQueryService, SupplierLookupQueryService>();
        services.AddScoped<IReceivableQueryService, ReceivableLookupQueryService>();

        // Application use cases
        services.AddScoped<SaveCustomerUseCase>();
        services.AddScoped<GetCustomerHistoryUseCase>();
        services.AddScoped<SearchCustomersUseCase>();
        services.AddScoped<SaveSupplierUseCase>();
        services.AddScoped<SearchSuppliersUseCase>();
        services.AddScoped<RegisterPurchaseUseCase>();
        services.AddScoped<CreateQuoteUseCase>();
        services.AddScoped<ConvertQuoteToSaleUseCase>();
        services.AddScoped<GetQuoteHistoryUseCase>();
        services.AddScoped<CreateSaleUseCase>();
        services.AddScoped<PreviewPaymentUseCase>();
        services.AddScoped<GetDashboardSummaryUseCase>();
        services.AddScoped<SendDocumentUseCase>();
        services.AddScoped<ResendDocumentUseCase>();
        services.AddScoped<IApplyReceivablePaymentUseCase, ApplyReceivablePaymentUseCase>();

        return services;
    }
}
