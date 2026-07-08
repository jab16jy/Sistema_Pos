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

        string? logoPath = ResolveBrandLogoPath();

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.4f, Unit.Centimetre);
                page.DefaultTextStyle(text => text.FontFamily("Segoe UI").FontSize(10).FontColor(Colors.Grey.Darken3));
                page.PageColor(Colors.Grey.Lighten5);

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Element(header => ComposeInvoiceHeader(header, settings, sale, customer, logoPath));

                    col.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Element(card => ComposeCustomerCard(card, customer));
                        row.RelativeItem().Element(card => ComposePaymentCard(card, sale, culture));
                    });

                    col.Item().Element(table => ComposeItemsTable(table, sale, culture));
                    col.Item().AlignRight().Width(260).Element(totals => ComposeTotalsCard(totals, sale, culture));
                    col.Item().Element(footer => ComposeInvoiceFooter(footer));
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    private static void ComposeInvoiceHeader(
        IContainer container,
        BusinessSettings? settings,
        Sale sale,
        Customer? customer,
        string? logoPath)
    {
        string businessName = settings?.BusinessName ?? "POS Local";

        container.Background(Colors.Blue.Darken4).Padding(18).Column(col =>
        {
            col.Spacing(10);
            col.Item().Row(row =>
            {
                row.Spacing(16);
                row.ConstantItem(76).Height(76).Element(logo => ComposeLogo(logo, logoPath));
                row.RelativeItem().Column(info =>
                {
                    info.Spacing(3);
                    info.Item().Text(businessName).FontSize(21).Bold().FontColor(Colors.White);
                    info.Item().Text("Factura de venta POS").FontSize(11).FontColor(Colors.Blue.Lighten4);

                    if (!string.IsNullOrWhiteSpace(settings?.BusinessAddress))
                    {
                        info.Item().Text(settings.BusinessAddress).FontSize(9).FontColor(Colors.Grey.Lighten2);
                    }

                    if (!string.IsNullOrWhiteSpace(settings?.TaxId))
                    {
                        info.Item().Text($"NIT: {settings.TaxId}").FontSize(9).FontColor(Colors.Grey.Lighten2);
                    }
                });

                row.ConstantItem(170).AlignRight().Column(meta =>
                {
                    meta.Spacing(4);
                    meta.Item().AlignRight().Text("FACTURA").FontSize(10).Bold().FontColor(Colors.Blue.Lighten4);
                    meta.Item().AlignRight().Text(sale.InvoiceNumber).FontSize(15).Bold().FontColor(Colors.White);
                    meta.Item().AlignRight().Text(sale.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).FontSize(9).FontColor(Colors.Grey.Lighten2);
                    meta.Item().AlignRight().Text(customer is null ? "Cliente consumidor final" : customer.FullName).FontSize(9).FontColor(Colors.Grey.Lighten2);
                });
            });
        });
    }

    private static void ComposeLogo(IContainer container, string? logoPath)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            container.Background(Colors.White).Padding(6).Image(File.ReadAllBytes(logoPath)).FitArea();
            return;
        }

        container.Background(Colors.White).AlignCenter().AlignMiddle()
            .Text("POS").FontSize(18).Bold().FontColor(Colors.Blue.Darken4);
    }

    private static void ComposeCustomerCard(IContainer container, Customer? customer)
    {
        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Cliente").FontSize(9).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().Text(customer?.FullName ?? "Consumidor final").FontSize(12).Bold();

            if (!string.IsNullOrWhiteSpace(customer?.Identification))
            {
                col.Item().Text($"Documento: {customer.Identification}").FontSize(9);
            }

            if (!string.IsNullOrWhiteSpace(customer?.Phone))
            {
                col.Item().Text($"Teléfono: {customer.Phone}").FontSize(9);
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
            col.Item().Text("Pago").FontSize(9).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().Text(string.Join(", ", paymentMethods)).FontSize(12).Bold();

            if (cashPayment is not null)
            {
                decimal received = cashPayment.ReceivedAmount?.Amount ?? cashPayment.Amount.Amount;
                col.Item().Text($"Recibido: {received.ToString("C0", culture)}").FontSize(9);
                col.Item().Text($"Cambio: {cashPayment.ChangeAmount().Amount.ToString("C0", culture)}").FontSize(9);
            }
        });
    }

    private static void ComposeItemsTable(IContainer container, Sale sale, CultureInfo culture)
    {
        container.Element(CardStyle).Table(table =>
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
                header.Cell().Element(HeaderCellStyle).Text("Descripción").FontColor(Colors.White).Bold();
                header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").FontColor(Colors.White).Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Precio unit.").FontColor(Colors.White).Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold();
            });

            foreach (var item in sale.Items)
            {
                table.Cell().Element(CellStyle).Text(item.ProductName).Bold();
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString("N2", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal().Amount.ToString("C0", culture));
            }
        });
    }

    private static void ComposeTotalsCard(IContainer container, Sale sale, CultureInfo culture)
    {
        container.Background(Colors.Blue.Lighten5).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(12).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Base gravable");
                row.ConstantItem(100).AlignRight().Text(sale.TaxableBaseTotal().Amount.ToString("C0", culture));
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("IVA incluido");
                row.ConstantItem(100).AlignRight().Text(sale.TaxTotal().Amount.ToString("C0", culture));
            });
            col.Item().LineHorizontal(1).LineColor(Colors.Blue.Lighten3);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("TOTAL").FontSize(13).Bold().FontColor(Colors.Blue.Darken4);
                row.ConstantItem(120).AlignRight().Text(sale.Total().Amount.ToString("C0", culture)).FontSize(15).Bold().FontColor(Colors.Blue.Darken4);
            });
        });
    }

    private static void ComposeInvoiceFooter(IContainer container)
    {
        container.AlignCenter().Column(col =>
        {
            col.Spacing(3);
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            col.Item().AlignCenter().Text("Gracias por su compra").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().AlignCenter().Text($"Documento generado por POS Local — {DateTimeOffset.Now:yyyy-MM-dd}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static IContainer CardStyle(IContainer container) =>
        container.Background(Colors.White).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12);

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(Colors.Blue.Darken3).PaddingVertical(7).PaddingHorizontal(6);

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(6).PaddingHorizontal(6);

    private static string PaymentMethodName(Domain.Enums.PaymentMethod method) => method switch
    {
        Domain.Enums.PaymentMethod.Cash       => "Efectivo",
        Domain.Enums.PaymentMethod.DebitCard  => "Tarjeta débito",
        Domain.Enums.PaymentMethod.CreditCard => "Tarjeta crédito",
        Domain.Enums.PaymentMethod.Transfer   => "Transferencia",
        _                                     => method.ToString()
    };

    private static string? ResolveBrandLogoPath()
    {
        foreach (string root in EnumerateCandidateRoots())
        {
            string newLogo = Path.Combine(root, "logo", "Newlogo.png");
            if (File.Exists(newLogo))
            {
                return newLogo;
            }

            string presentationLogo = Path.Combine(root, "src", "POS.Presentation", "Resources", "logo.jpeg");
            if (File.Exists(presentationLogo))
            {
                return presentationLogo;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(start);
            for (int depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }
            }
        }
    }
}
