using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Quotes;
using POS.Domain.Interfaces;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPosInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IDocumentSendLogRepository, DocumentSendLogRepository>();

        // Infrastructure services
        services.AddScoped<IPrintService, PrintService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IWhatsAppService, WhatsAppScriptService>();

        // Application use cases
        services.AddScoped<CreateQuoteUseCase>();
        services.AddScoped<ConvertQuoteToSaleUseCase>();
        services.AddScoped<GetQuoteHistoryUseCase>();
        services.AddScoped<SendDocumentUseCase>();
        services.AddScoped<ResendDocumentUseCase>();

        return services;
    }
}
