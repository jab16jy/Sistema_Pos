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
            .AsNoTracking()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct)
            .ConfigureAwait(false);

        if (quote is null)
        {
            throw new InvalidOperationException($"Cotización {quoteId} no encontrada.");
        }

        var settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var culture = CultureInfo.GetCultureInfo("es-CO");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);
                page.Content().Scale(ContentScale).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(header => ComposeDocumentHeader(
                        header, settings, "COTIZACIÓN", quote.QuoteNumber, quote.CreatedAt, quote.CustomerName));
                    col.Item().Element(card => ComposeQuoteMetaCard(card, quote));
                    col.Item().Element(table => ComposeQuoteItemsTable(table, quote, culture));
                    col.Item().AlignRight().Width(260).Element(totals => ComposeTotalRow(totals, "TOTAL", quote.Total.ToString("C0", culture)));
                    if (!string.IsNullOrWhiteSpace(quote.Observations))
                    {
                        col.Item().Element(CardStyle).Column(obs =>
                        {
                            obs.Item().Text("Observaciones").FontSize(9).Bold().FontColor(LabelColor);
                            obs.Item().Text(quote.Observations).FontSize(10);
                        });
                    }
                    col.Item().Element(footer => ComposeDocumentFooter(footer, "Cotización sujeta a disponibilidad y vigencia."));
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
                ConfigurePage(page);
                page.Content().Scale(ContentScale).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Element(header => ComposeDocumentHeader(
                        header, settings, "FACTURA", sale.InvoiceNumber, sale.CreatedAt,
                        customer?.FullName ?? "Cliente consumidor final"));

                    col.Item().Element(card => ComposeCustomerCard(card, customer));
                    col.Item().Element(card => ComposePaymentCard(card, sale, culture));
                    col.Item().Element(table => ComposeItemsTable(table, sale, culture));
                    col.Item().AlignRight().Width(280).Element(totals => ComposeTotalsCard(totals, sale, culture));
                    col.Item().Element(footer => ComposeDocumentFooter(footer, "Gracias por su compra"));
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    // ponytail: one clean monochrome layout for both documents — no color fills, thermal/print friendly.
    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.4f, Unit.Centimetre);
        page.DefaultTextStyle(text => text.FontFamily("Segoe UI").FontSize(10).FontColor(BodyColor));
        page.PageColor(Colors.White);
    }

    private static void ComposeDocumentHeader(
        IContainer container,
        BusinessSettings? settings,
        string documentType,
        string documentNumber,
        DateTimeOffset createdAt,
        string partyName)
    {
        string businessName = string.IsNullOrWhiteSpace(settings?.BusinessName) ? "ESTHETIC MEDICAL STORE" : settings!.BusinessName;

        container.Element(CardStyle).Row(row =>
        {
            row.RelativeItem().Column(info =>
            {
                info.Spacing(2);
                info.Item().Text(businessName).FontSize(20).Bold().FontColor(BodyColor);
                info.Item().Text($"{documentType.Substring(0, 1)}{documentType.Substring(1).ToLowerInvariant()} de venta POS").FontSize(10).FontColor(LabelColor);

                if (!string.IsNullOrWhiteSpace(settings?.BusinessAddress))
                {
                    info.Item().Text(settings.BusinessAddress).FontSize(9).FontColor(LabelColor);
                }
                if (!string.IsNullOrWhiteSpace(settings?.BusinessPhone))
                {
                    info.Item().Text($"Tel: {settings.BusinessPhone}").FontSize(9).FontColor(LabelColor);
                }
                if (!string.IsNullOrWhiteSpace(settings?.TaxId))
                {
                    info.Item().Text($"NIT: {settings.TaxId}").FontSize(9).FontColor(LabelColor);
                }
            });

            row.ConstantItem(200).Column(meta =>
            {
                meta.Spacing(2);
                meta.Item().AlignRight().Text(documentType).FontSize(11).Bold().FontColor(LabelColor);
                meta.Item().AlignRight().Text(documentNumber).FontSize(16).Bold().FontColor(BodyColor);
                meta.Item().AlignRight().Text(createdAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).FontSize(9).FontColor(LabelColor);
                meta.Item().AlignRight().Text(partyName).FontSize(9).FontColor(LabelColor);
            });
        });
    }

    private static void ComposeQuoteMetaCard(IContainer container, Quote quote)
    {
        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Datos de la cotización").FontSize(9).Bold().FontColor(LabelColor);
            col.Item().Text(quote.CustomerName).FontSize(12).Bold();
            col.Item().Text($"Válida hasta: {quote.ExpiresAt:yyyy-MM-dd}").FontSize(9);
        });
    }

    private static void ComposeCustomerCard(IContainer container, Customer? customer)
    {
        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Datos del cliente").FontSize(9).Bold().FontColor(LabelColor);
            col.Item().Text(customer?.FullName ?? "Consumidor final").FontSize(12).Bold();

            if (!string.IsNullOrWhiteSpace(customer?.Identification))
            {
                col.Item().Text($"Cédula/NIT: {customer.Identification}").FontSize(9);
            }
            if (!string.IsNullOrWhiteSpace(customer?.Phone))
            {
                col.Item().Text($"Teléfono: {customer.Phone}").FontSize(9);
            }
            if (!string.IsNullOrWhiteSpace(customer?.Email))
            {
                col.Item().Text($"Email: {customer.Email}").FontSize(9);
            }
            if (!string.IsNullOrWhiteSpace(customer?.Address))
            {
                col.Item().Text($"Dirección: {customer.Address}").FontSize(9);
            }
        });
    }

    private static void ComposePaymentCard(IContainer container, Sale sale, CultureInfo culture)
    {
        var paymentMethods = sale.Payments.Select(p => PaymentMethodName(p.Method)).Distinct();
        var cashPayment = sale.Payments.FirstOrDefault(p => p.Method == Domain.Enums.PaymentMethod.Cash);

        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Forma de pago").FontSize(9).Bold().FontColor(LabelColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(string.Join(", ", paymentMethods)).FontSize(12).Bold();
                row.ConstantItem(120).AlignRight().Text(sale.TotalPaid().Amount.ToString("C0", culture)).FontSize(12).Bold();
            });

            if (cashPayment is not null)
            {
                decimal received = cashPayment.ReceivedAmount?.Amount ?? cashPayment.Amount.Amount;
                col.Item().Text($"Recibido: {received.ToString("C0", culture)} · Cambio: {cashPayment.ChangeAmount().Amount.ToString("C0", culture)}").FontSize(9).FontColor(LabelColor);
            }

            col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(BorderColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total pagado").FontSize(11).Bold();
                row.ConstantItem(120).AlignRight().Text(sale.TotalPaid().Amount.ToString("C0", culture)).FontSize(11).Bold();
            });
        });
    }

    private static void ComposeItemsTable(IContainer container, Sale sale, CultureInfo culture)
    {
        container.Element(CardStyle).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(64);
                columns.RelativeColumn(4);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("ID").Bold();
                header.Cell().Element(HeaderCellStyle).Text("Producto").Bold();
                header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Vr unit.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("IVA").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total").Bold();
            });

            foreach (var item in sale.Items)
            {
                string shortId = item.ProductId.ToString("N")[..8].ToUpperInvariant();
                table.Cell().Element(CellStyle).Text(shortId).FontSize(9).FontColor(LabelColor);
                table.Cell().Element(CellStyle).Text(item.ProductName).Bold();
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString("N2", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.TaxAmount().Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal().Amount.ToString("C0", culture));
            }
        });
    }

    private static void ComposeQuoteItemsTable(IContainer container, Quote quote, CultureInfo culture)
    {
        container.Element(CardStyle).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Producto").Bold();
                header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Vr unit.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total").Bold();
            });

            foreach (var item in quote.Items)
            {
                table.Cell().Element(CellStyle).Text(item.ProductName).Bold();
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.Value.ToString("N2", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal().Amount.ToString("C0", culture));
            }
        });
    }

    private static void ComposeTotalsCard(IContainer container, Sale sale, CultureInfo culture)
    {
        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total bruto");
                row.ConstantItem(120).AlignRight().Text(sale.Total().Amount.ToString("C0", culture));
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Subtotal");
                row.ConstantItem(120).AlignRight().Text(sale.TaxableBaseTotal().Amount.ToString("C0", culture));
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("IVA 19% incluido");
                row.ConstantItem(120).AlignRight().Text(sale.TaxTotal().Amount.ToString("C0", culture));
            });
            col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("TOTAL A PAGAR").FontSize(13).Bold();
                row.ConstantItem(120).AlignRight().Text(sale.Total().Amount.ToString("C0", culture)).FontSize(15).Bold();
            });
        });
    }

    private static void ComposeTotalRow(IContainer container, string label, string amount)
    {
        container.Element(CardStyle).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(13).Bold();
            row.ConstantItem(120).AlignRight().Text(amount).FontSize(15).Bold();
        });
    }

    private static void ComposeDocumentFooter(IContainer container, string message)
    {
        container.AlignCenter().Column(col =>
        {
            col.Spacing(3);
            col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
            col.Item().AlignCenter().Text(message).FontSize(11).Bold().FontColor(BodyColor);
            col.Item().AlignCenter().Text($"Documento generado por POS Local — {DateTimeOffset.Now:yyyy-MM-dd}").FontSize(8).FontColor(LabelColor);
        });
    }

    // ponytail: single knob to shrink the whole document (fonts + spacing) — QuestPDF Scale does it proportionally.
    private const float ContentScale = 0.75f;
    private const string BodyColor = "#1A1A1A";
    private const string LabelColor = "#6B6B6B";
    private const string BorderColor = "#D0D0D0";

    private static IContainer CardStyle(IContainer container) =>
        container.Border(0.75f).BorderColor(BorderColor).Padding(12);

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background("#F2F2F2").BorderBottom(0.75f).BorderColor(BorderColor).PaddingVertical(6).PaddingHorizontal(6);

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor("#ECECEC").PaddingVertical(6).PaddingHorizontal(6);

    private static string PaymentMethodName(Domain.Enums.PaymentMethod method) => method switch
    {
        Domain.Enums.PaymentMethod.Cash => "Efectivo",
        Domain.Enums.PaymentMethod.DebitCard => "Tarjeta débito",
        Domain.Enums.PaymentMethod.CreditCard => "Tarjeta crédito",
        Domain.Enums.PaymentMethod.Transfer => "Transferencia",
        Domain.Enums.PaymentMethod.LoyaltyPoints => "Puntos de lealtad",
        _ => method.ToString()
    };
}
