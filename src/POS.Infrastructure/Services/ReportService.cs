using System.Globalization;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;

    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<byte[]> GenerateDailySalesReportAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        DateTimeOffset from = new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        DateTimeOffset to = new(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var salesInStore = await _dbContext.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sales = salesInStore
            .Where(sale => sale.Status == SaleStatus.Completed &&
                           sale.CreatedAt >= from &&
                           sale.CreatedAt <= to)
            .OrderBy(sale => sale.CreatedAt)
            .ToList();

        decimal billedTotal = sales.Sum(sale => sale.Total().Amount);
        decimal cashTotal = sales.Sum(sale => sale.CashInDrawer().Amount);
        decimal digitalTotal = sales
            .SelectMany(sale => sale.Payments)
            .Where(payment => payment.Method != PaymentMethod.Cash)
            .Sum(payment => payment.Amount.Amount);
        var culture = CultureInfo.GetCultureInfo("es-CO");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Column(column =>
                {
                    column.Item().Text($"Reporte diario de ventas - {date:yyyy-MM-dd}").FontSize(18).Bold();
                    column.Item().PaddingTop(8).Text($"Ventas completadas: {sales.Count}");
                    column.Item().Text($"Total facturado: {billedTotal.ToString("C0", culture)}");
                    column.Item().Text($"Efectivo esperado en caja: {cashTotal.ToString("C0", culture)}");
                    column.Item().Text($"Pagos digitales: {digitalTotal.ToString("C0", culture)}");

                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Factura").Bold();
                            header.Cell().Text("Hora").Bold();
                            header.Cell().Text("Total").Bold();
                            header.Cell().Text("Efectivo").Bold();
                        });

                        foreach (var sale in sales)
                        {
                            table.Cell().Text(sale.InvoiceNumber);
                            table.Cell().Text(sale.CreatedAt.ToLocalTime().ToString("HH:mm", culture));
                            table.Cell().Text(sale.Total().Amount.ToString("C0", culture));
                            table.Cell().Text(sale.CashInDrawer().Amount.ToString("C0", culture));
                        }
                    });
                });
            });
        }).GeneratePdf();
    }
}
