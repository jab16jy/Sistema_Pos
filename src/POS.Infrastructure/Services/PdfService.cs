using System.Globalization;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Services;

public sealed class PdfService : IPdfService
{
    private readonly AppDbContext _dbContext;

    static PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> GenerateQuotePdfAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _dbContext.Quotes
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote is null)
        {
            throw new InvalidOperationException($"Cotización {quoteId} no encontrada.");
        }

        var culture = CultureInfo.GetCultureInfo("es-CO");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text($"COTIZACIÓN {quote.QuoteNumber}").FontSize(18).Bold();
                    col.Item().Text($"Cliente: {quote.CustomerName}");
                    col.Item().Text($"Fecha: {quote.CreatedAt:yyyy-MM-dd}");
                    col.Item().Text($"Válida hasta: {quote.ExpiresAt:yyyy-MM-dd}");
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Producto").Bold();
                            header.Cell().Text("Cant.").Bold();
                            header.Cell().Text("Precio Unit.").Bold();
                            header.Cell().Text("Subtotal").Bold();
                        });
                        foreach (var item in quote.Items)
                        {
                            table.Cell().Text(item.ProductName);
                            table.Cell().Text(item.Quantity.Value.ToString("N2"));
                            table.Cell().Text(item.UnitPrice.Amount.ToString("C0", culture));
                            table.Cell().Text(item.Subtotal().Amount.ToString("C0", culture));
                        }
                    });
                    col.Item().PaddingTop(10).Text($"TOTAL: {quote.Total.ToString("C0", culture)}").Bold().FontSize(14);
                    if (!string.IsNullOrEmpty(quote.Observations))
                    {
                        col.Item().PaddingTop(5).Text($"Observaciones: {quote.Observations}");
                    }
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    public Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default)
    {
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text($"FACTURA\nID: {saleId}").FontSize(18);
            });
        }).GeneratePdf();

        return Task.FromResult(pdfBytes);
    }
}
