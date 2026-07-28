using System.Globalization;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
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
    private const int ThermalLogoWidthDots = 280;
    private const int ThermalLogoHeightDots = 213;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private readonly AppDbContext _dbContext;
    private static readonly Lazy<byte[]> LogoImage = new(LoadLogoImage);

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

        Customer? customer = await FindCustomerAsync(quote.CustomerId, ct).ConfigureAwait(false);
        LoyaltyTier? loyaltyTier = await FindLoyaltyTierAsync(quote.CustomerId, ct).ConfigureAwait(false);

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
                    col.Item().Element(card => ComposeQuoteMetaCard(card, quote, customer, loyaltyTier));
                    col.Item().Element(table => ComposeQuoteItemsTable(table, quote, culture));
                    col.Item().AlignRight().Width(280).Element(totals => ComposeQuoteTotalsCard(totals, quote, culture));
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

        Customer? customer = await FindCustomerAsync(sale.CustomerId, ct).ConfigureAwait(false);
        LoyaltySaleSnapshot loyalty = await LoyaltySaleSnapshot
            .LoadAsync(_dbContext, sale.CustomerId, sale.Id, ct)
            .ConfigureAwait(false);

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
                    if (loyalty.HasContent || LoyaltyDiscount(sale) > 0m)
                    {
                        col.Item().Element(card => ComposeLoyaltyCard(card, loyalty, LoyaltyDiscount(sale), culture));
                    }
                    col.Item().Element(card => ComposePaymentCard(card, sale, culture));
                    col.Item().Element(table => ComposeItemsTable(table, sale, culture));
                    col.Item().AlignRight().Width(280).Element(totals => ComposeTotalsCard(totals, sale, culture));
                    col.Item().Element(footer => ComposeDocumentFooter(footer, "Gracias por su compra"));
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

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
            if (LogoImage.Value.Length > 0)
            {
                row.ConstantItem(76).Height(64).Image(LogoImage.Value).FitArea();
            }

            row.RelativeItem().Column(info =>
            {
                info.Spacing(2);
                info.Item().Text(businessName).FontSize(20).Bold().FontColor(BodyColor);
                info.Item().Text($"{documentType} · Documento comercial").FontSize(10).FontColor(LabelColor);

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

    private static void ComposeQuoteMetaCard(IContainer container, Quote quote, Customer? customer, LoyaltyTier? loyaltyTier)
    {
        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Datos de la cotización").FontSize(9).Bold().FontColor(LabelColor);
            col.Item().Text(quote.CustomerName).FontSize(12).Bold();
            col.Item().Text($"Válida hasta: {quote.ExpiresAt:yyyy-MM-dd} · Vigencia: {quote.ValidityDays} días").FontSize(9);
            if (!string.IsNullOrWhiteSpace(quote.CustomerPhone))
                col.Item().Text($"Teléfono: {quote.CustomerPhone}").FontSize(9);
            if (!string.IsNullOrWhiteSpace(quote.CustomerEmail))
                col.Item().Text($"Email: {quote.CustomerEmail}").FontSize(9);
            if (loyaltyTier is not null)
                col.Item().Text($"Nivel de lealtad actual: {loyaltyTier.Name} · Beneficio: {loyaltyTier.DiscountPercent:N0}% dcto.").FontSize(9).FontColor(AccentColor);
        });
    }

    private static void ComposeLoyaltyCard(IContainer container, LoyaltySaleSnapshot loyalty, decimal loyaltyDiscount, CultureInfo culture)
    {
        container.Element(AccentCardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Puntos y beneficios de lealtad").FontSize(9).Bold().FontColor(AccentColor);

            if (loyalty.PointsEarned != 0)
                col.Item().Text($"Puntos ganados en esta compra: {loyalty.PointsEarned.ToString("N0", culture)}").FontSize(10).Bold();

            // "a hoy" because a reprint reads the live balance, which has moved on since the sale.
            col.Item().Text($"Puntos acumulados (a hoy): {loyalty.Balance.ToString("N0", culture)}").FontSize(10).Bold();

            if (loyalty.Tier is { } tier)
            {
                col.Item().Text($"Nivel actual: {tier.Name}").FontSize(10).Bold();
                col.Item().Text($"· {tier.DiscountPercent:N0}% de descuento · x{tier.PointsMultiplier:0.##} puntos").FontSize(9);
                foreach (var benefit in tier.BenefitLines)
                    col.Item().Text($"· {benefit}").FontSize(9);
            }

            if (loyaltyDiscount > 0m)
                col.Item().Text($"Canje de puntos aplicado: -{loyaltyDiscount.ToString("C0", culture)}").FontSize(10).Bold();
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
        decimal loyaltyDiscount = LoyaltyDiscount(sale);
        decimal totalPaid = sale.TotalPaid().Amount - loyaltyDiscount;

        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Forma de pago").FontSize(9).Bold().FontColor(LabelColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(string.Join(", ", paymentMethods.Where(method => method != "Puntos de lealtad"))).FontSize(12).Bold();
                row.ConstantItem(120).AlignRight().Text(totalPaid.ToString("C0", culture)).FontSize(12).Bold();
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
                row.ConstantItem(120).AlignRight().Text(totalPaid.ToString("C0", culture)).FontSize(11).Bold();
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
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("ID").Bold();
                header.Cell().Element(HeaderCellStyle).Text("Producto").Bold();
                header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Vr unit.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Dcto.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("IVA").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total").Bold();
            });

            foreach (var item in sale.Items)
            {
                string shortId = item.ProductId.ToString("N")[..8].ToUpperInvariant();
                table.Cell().Element(CellStyle).Text(shortId).FontSize(9).FontColor(LabelColor);
                table.Cell().Element(CellStyle).Column(column =>
                {
                    column.Item().Text(item.ProductName).Bold();
                    column.Item().Text(PresentationLabel(item.Presentation, item.UnitsPerPresentation)).FontSize(8).FontColor(LabelColor);
                });
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString("N2", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(ItemDiscount(item).ToString("C0", culture));
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
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Producto").Bold();
                header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Vr unit.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Dcto.").Bold();
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total").Bold();
            });

            foreach (var item in quote.Items)
            {
                table.Cell().Element(CellStyle).Column(column =>
                {
                    column.Item().Text(item.ProductName).Bold();
                    column.Item().Text(PresentationLabel(item.Presentation, item.UnitsPerPresentation)).FontSize(8).FontColor(LabelColor);
                });
                table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.Value.ToString("N2", culture));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.Amount.ToString("C0", culture));
                table.Cell().Element(CellStyle).AlignRight().Text($"{item.Discount:N0}%");
                table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal().Amount.ToString("C0", culture));
            }
        });
    }

    private static void ComposeTotalsCard(IContainer container, Sale sale, CultureInfo culture)
    {
        decimal loyaltyDiscount = LoyaltyDiscount(sale);
        decimal grossTotal = GrossTotal(sale);
        decimal itemDiscount = grossTotal - sale.Total().Amount;

        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total bruto");
                row.ConstantItem(120).AlignRight().Text(grossTotal.ToString("C0", culture));
            });
            if (itemDiscount > 0m)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Descuentos en productos").FontColor(AccentColor);
                    row.ConstantItem(120).AlignRight().Text($"-{itemDiscount.ToString("C0", culture)}").FontColor(AccentColor);
                });
            }
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

            if (loyaltyDiscount > 0m)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Canje de puntos").FontColor(AccentColor);
                    row.ConstantItem(120).AlignRight().Text($"-{loyaltyDiscount.ToString("C0", culture)}").FontColor(AccentColor);
                });
            }

            col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("TOTAL A PAGAR").FontSize(13).Bold();
                row.ConstantItem(120).AlignRight().Text((sale.Total().Amount - loyaltyDiscount).ToString("C0", culture)).FontSize(15).Bold();
            });
        });
    }

    private static void ComposeQuoteTotalsCard(IContainer container, Quote quote, CultureInfo culture)
    {
        decimal grossTotal = quote.Items.Sum(item => item.UnitPrice.Amount * item.Quantity.Value);
        decimal itemDiscount = grossTotal - quote.Total;

        container.Element(CardStyle).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total bruto");
                row.ConstantItem(120).AlignRight().Text(grossTotal.ToString("C0", culture));
            });
            if (itemDiscount > 0m)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Descuentos en productos").FontColor(AccentColor);
                    row.ConstantItem(120).AlignRight().Text($"-{itemDiscount.ToString("C0", culture)}").FontColor(AccentColor);
                });
            }
            col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("TOTAL COTIZADO").FontSize(13).Bold();
                row.ConstantItem(120).AlignRight().Text(quote.Total.ToString("C0", culture)).FontSize(15).Bold();
            });
        });
    }

    private static decimal LoyaltyDiscount(Sale sale) => sale.Payments
        .Where(payment => payment.Method == Domain.Enums.PaymentMethod.LoyaltyPoints)
        .Sum(payment => payment.Amount.Amount);

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

    private const float ContentScale = 0.75f;
    private const string BodyColor = "#1A1A1A";
    private const string LabelColor = "#6B6B6B";
    private const string BorderColor = "#D0D0D0";
    private const string AccentColor = "#0F766E";

    private static IContainer CardStyle(IContainer container) =>
        container.Border(0.75f).BorderColor(BorderColor).Padding(12);

    private static IContainer AccentCardStyle(IContainer container) =>
        container.Background("#F0FDFA").Border(0.75f).BorderColor("#99F6E4").Padding(12);

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

    private static string PresentationLabel(Domain.Enums.ProductPresentation presentation, int unitsPerPresentation) =>
        presentation == Domain.Enums.ProductPresentation.Box ? $"Caja x{unitsPerPresentation}" : "Unidad";

    private async Task<Customer?> FindCustomerAsync(Guid? customerId, CancellationToken ct) => customerId.HasValue
        ? await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.Id == customerId.Value, ct).ConfigureAwait(false)
        : null;

    /// <summary>
    /// Resolves the tier the customer's point balance qualifies for. Quotes have no sale, so the
    /// snapshot is used purely for its tier — see <see cref="LoyaltySaleSnapshot"/> for why the
    /// tier is derived from the balance instead of Customer.LoyaltyTierId.
    /// </summary>
    private async Task<LoyaltyTier?> FindLoyaltyTierAsync(Guid? customerId, CancellationToken ct) =>
        (await LoyaltySaleSnapshot.LoadAsync(_dbContext, customerId, Guid.Empty, ct).ConfigureAwait(false)).Tier;

    private static decimal GrossTotal(Sale sale) => sale.Items.Sum(item => item.UnitPrice.Amount * item.Quantity);

    private static decimal ItemDiscount(SaleItem item) => item.UnitPrice.Amount * item.Quantity - item.Subtotal().Amount;

    private static byte[] LoadLogoImage()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("POS.Infrastructure.Assets.receipt-logo.bin");
        if (stream is null)
            return [];

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return BuildPngImage(memory.ToArray(), ThermalLogoWidthDots, ThermalLogoHeightDots);
    }

    private static byte[] BuildPngImage(byte[] packedBits, int width, int height)
    {
        int widthBytes = (width + 7) / 8;
        if (packedBits.Length < widthBytes * height)
            return [];

        var scanlines = new byte[(width + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * widthBytes;
            int targetRow = y * (width + 1);
            scanlines[targetRow] = 0;

            for (int x = 0; x < width; x++)
            {
                bool isPrinted = (packedBits[sourceRow + x / 8] & (1 << (7 - x % 8))) != 0;
                scanlines[targetRow + x + 1] = isPrinted ? (byte)0 : (byte)255;
            }
        }

        using var png = new MemoryStream();
        png.Write(PngSignature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // grayscale
        WritePngChunk(png, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(scanlines);
        }

        WritePngChunk(png, "IDAT", compressed.ToArray());
        WritePngChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WritePngChunk(Stream destination, string type, ReadOnlySpan<byte> data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        destination.Write(length);
        destination.Write(typeBytes);
        destination.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, PngCrc(typeBytes, data));
        destination.Write(crc);
    }

    private static uint PngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in type)
            crc = UpdatePngCrc(crc, value);
        foreach (byte value in data)
            crc = UpdatePngCrc(crc, value);
        return ~crc;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
            crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        return crc;
    }
}
