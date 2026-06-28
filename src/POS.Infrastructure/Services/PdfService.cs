using System.Globalization;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Domain.Entities;
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

    public async Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false);

        if (sale is null)
            throw new InvalidOperationException($"Venta {saleId} no encontrada.");

        var settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var culture = CultureInfo.GetCultureInfo("es-CO");

        Customer? customer = null;
        if (sale.CustomerId.HasValue)
        {
            customer = await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value, ct)
                .ConfigureAwait(false);
        }

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Column(col =>
                {
                    // ── Business header ──────────────────────────────────────
                    col.Item().AlignCenter().Text(settings?.BusinessName ?? "POS Local")
                        .FontSize(20).Bold();
                    if (!string.IsNullOrWhiteSpace(settings?.BusinessAddress))
                        col.Item().AlignCenter().Text(settings.BusinessAddress).FontSize(11);
                    if (!string.IsNullOrWhiteSpace(settings?.TaxId))
                        col.Item().AlignCenter().Text($"NIT: {settings.TaxId}").FontSize(11);

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // ── Invoice metadata ─────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("FACTURA DE VENTA").FontSize(14).Bold();
                        row.RelativeItem().AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text($"N°: {sale.InvoiceNumber}").Bold();
                            metaCol.Item().Text($"Fecha: {sale.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                        });
                    });

                    if (customer is not null)
                    {
                        col.Item().PaddingTop(4).Text($"Cliente: {customer.FullName}");
                        if (!string.IsNullOrWhiteSpace(customer.Phone))
                            col.Item().Text($"Teléfono: {customer.Phone}").FontSize(10);
                    }

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // ── Items table ──────────────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Descripción").Bold();
                            header.Cell().Text("Cant.").Bold();
                            header.Cell().AlignRight().Text("Precio Unit.").Bold();
                            header.Cell().AlignRight().Text("Subtotal").Bold();
                        });

                        foreach (var item in sale.Items)
                        {
                            table.Cell().Text(item.ProductName);
                            table.Cell().Text(item.Quantity.ToString("N2", culture));
                            table.Cell().AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                            table.Cell().AlignRight().Text(item.Subtotal().Amount.ToString("C0", culture));
                        }
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // ── Totals ───────────────────────────────────────────────
                    col.Item().AlignRight()
                        .Text($"Base gravable: {sale.TaxableBaseTotal().Amount.ToString("C0", culture)}");
                    col.Item().AlignRight()
                        .Text($"IVA incluido: {sale.TaxTotal().Amount.ToString("C0", culture)}");
                    col.Item().AlignRight()
                        .Text($"TOTAL: {sale.Total().Amount.ToString("C0", culture)}")
                        .FontSize(14).Bold();

                    var cashPayment = sale.Payments.FirstOrDefault(
                        p => p.Method == Domain.Enums.PaymentMethod.Cash);
                    if (cashPayment is not null)
                    {
                        var received = cashPayment.ReceivedAmount?.Amount ?? cashPayment.Amount.Amount;
                        col.Item().AlignRight()
                            .Text($"Efectivo recibido: {received.ToString("C0", culture)}");
                        var change = cashPayment.ChangeAmount().Amount;
                        if (change > 0)
                            col.Item().AlignRight()
                                .Text($"Cambio: {change.ToString("C0", culture)}");
                    }

                    var methodNames = sale.Payments
                        .Select(p => p.Method switch
                        {
                            Domain.Enums.PaymentMethod.Cash       => "Efectivo",
                            Domain.Enums.PaymentMethod.DebitCard  => "Tarjeta Débito",
                            Domain.Enums.PaymentMethod.CreditCard => "Tarjeta Crédito",
                            Domain.Enums.PaymentMethod.Transfer   => "Transferencia",
                            _                                      => p.Method.ToString()
                        })
                        .Distinct();
                    col.Item().PaddingTop(4)
                        .Text($"Forma de pago: {string.Join(", ", methodNames)}");

                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // ── Footer ───────────────────────────────────────────────
                    col.Item().AlignCenter().PaddingTop(8)
                        .Text($"Documento generado por POS Local — {DateTimeOffset.Now:yyyy-MM-dd}")
                        .FontSize(9).Italic();
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }
}
