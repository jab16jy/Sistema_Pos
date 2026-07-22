using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

/// <summary>
/// ESC/POS thermal printer service. Resolves the configured target (installed Windows printer
/// name, COM3, 192.168.1.100:9100, USB path, or a legacy command) via <see cref="IPrinterSettingsResolver"/>
/// and sends the receipt bytes through <see cref="IThermalPrinterTransport"/>, falling back to
/// launching an external process when the target isn't a direct ESC/POS channel.
/// </summary>
public sealed partial class EscPosPrinterService : IPrintService
{
    private static readonly byte[] CmdDoubleSize = [0x1D, 0x21, 0x11];
    private static readonly byte[] CmdNormalSize = [0x1D, 0x21, 0x00];

    private static readonly CultureInfo CopCulture = CultureInfo.GetCultureInfo("es-CO");

    // ponytail: logo.bin is a pre-rendered 1bpp raster (280x213 dots) baked from logo/Newlogo.png —
    // no image library needed at runtime. Regenerate it offline if the logo artwork changes.
    private const int LogoWidthDots = 280;
    private const int LogoHeightDots = 213;
    private static readonly Lazy<byte[]> LogoRasterCommand = new(BuildLogoRasterCommand);

    private static byte[] BuildLogoRasterCommand()
    {
        using Stream? stream = typeof(EscPosPrinterService).Assembly
            .GetManifestResourceStream("POS.Infrastructure.Assets.receipt-logo.bin");
        if (stream is null)
            return [];

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return EscPosCommands.BuildRasterImageCommand(memory.ToArray(), LogoWidthDots, LogoHeightDots);
    }

    private readonly IPrinterSettingsResolver _printerSettingsResolver;
    private readonly IThermalPrinterTransport _transport;
    private readonly AppDbContext _dbContext;

    public EscPosPrinterService(
        IPrinterSettingsResolver printerSettingsResolver,
        IThermalPrinterTransport transport,
        AppDbContext dbContext)
    {
        _printerSettingsResolver = printerSettingsResolver ?? throw new ArgumentNullException(nameof(printerSettingsResolver));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken)
            .ConfigureAwait(false);

        if (sale is null)
            throw new InvalidOperationException($"Venta {saleId} no encontrada.");

        var settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        Customer? customer = sale.CustomerId.HasValue
            ? await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value, cancellationToken)
                .ConfigureAwait(false)
            : null;

        LoyaltyTier? loyaltyTier = customer?.LoyaltyTierId is Guid tierId
            ? await _dbContext.LoyaltyTiers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tierId, cancellationToken)
                .ConfigureAwait(false)
            : null;

        var receiptBytes = BuildReceiptBytes(sale, settings, customer, loyaltyTier);
        ThermalPrinterTarget target = await _printerSettingsResolver.ResolveRequiredAsync(cancellationToken).ConfigureAwait(false);

        if (target.SupportsDirectEscPos)
        {
            await _transport.SendAsync(target, receiptBytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        await PrintViaProcessAsync(target, saleId, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildReceiptBytes(Sale sale, BusinessSettings? settings, Customer? customer, LoyaltyTier? loyaltyTier)
    {
        var buf = new List<byte>();
        void Write(byte[] b) => buf.AddRange(b);
        void WriteText(string text) => buf.AddRange(EscPosCommands.Encode(text));
        void WriteLine(string text = "") => WriteText(text + "\n");
        void Separator() => WriteLine("--------------------------------");

        Write(EscPosCommands.Init);
        Write(EscPosCommands.SelectCodePageWpc1252);

        // ── Logo ────────────────────────────────────────────────────────────
        Write(EscPosCommands.CenterAlign);
        if (LogoRasterCommand.Value.Length > 0)
        {
            Write(LogoRasterCommand.Value);
            WriteLine();
        }

        // ── Header ──────────────────────────────────────────────────────────
        Write(EscPosCommands.BoldOn);
        Write(CmdDoubleSize);
        WriteLine(settings?.BusinessName ?? "POS Local");
        Write(CmdNormalSize);
        Write(EscPosCommands.BoldOff);

        if (!string.IsNullOrWhiteSpace(settings?.BusinessAddress))
            WriteLine(settings.BusinessAddress);
        if (!string.IsNullOrWhiteSpace(settings?.BusinessPhone))
            WriteLine($"Tel: {settings.BusinessPhone}");
        if (!string.IsNullOrWhiteSpace(settings?.TaxId))
            WriteLine($"NIT: {settings.TaxId}");

        WriteLine();

        // ── Invoice metadata ─────────────────────────────────────────────────
        Write(EscPosCommands.LeftAlign);
        Separator();
        WriteLine($"Factura: {sale.InvoiceNumber}");
        WriteLine($"Fecha:   {sale.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        WriteLine();

        if (customer is not null)
        {
            WriteLine($"Cliente: {customer.FullName}");
            if (!string.IsNullOrWhiteSpace(customer.Identification))
                WriteLine($"Cedula/NIT: {customer.Identification}");
            if (!string.IsNullOrWhiteSpace(customer.Phone))
                WriteLine($"Telefono: {customer.Phone}");
            if (!string.IsNullOrWhiteSpace(customer.Email))
                WriteLine($"Email: {customer.Email}");
            if (!string.IsNullOrWhiteSpace(customer.Address))
                WriteLine($"Direccion: {customer.Address}");
            if (loyaltyTier is not null)
                WriteLine($"Nivel: {loyaltyTier.Name} ({loyaltyTier.DiscountPercent:N0}% dcto)");
            WriteLine();
        }

        Separator();
        WriteLine();

        // ── Line items ───────────────────────────────────────────────────────
        foreach (var item in sale.Items)
        {
            var name = item.ProductName.Length > 20
                ? item.ProductName[..20]
                : item.ProductName;

            var qty = $"x{item.Quantity.ToString("0.##", CopCulture)}";
            var subtotal = FormatCurrency(item.Subtotal().Amount);

            // 32-char line: name(20) qty(5) price(7)
            var line = $"{name,-20}{qty,5} {subtotal,6}";
            WriteLine(line);
            WriteLine($"  {PresentationLabel(item.Presentation, item.UnitsPerPresentation)} · Vr unit: {FormatCurrency(item.UnitPrice.Amount)}");
            if (item.Discount.Amount > 0)
                WriteLine($"  Dcto: -{FormatCurrency(item.Discount.Amount)}");
            WriteLine();
        }

        Separator();
        WriteLine();

        // ── Totals ───────────────────────────────────────────────────────────
        var itemDiscountTotal = sale.Items.Sum(i => i.Discount.Amount);
        if (itemDiscountTotal > 0)
        {
            var grossTotal = sale.Items.Sum(i => i.UnitPrice.Amount * i.Quantity);
            WriteLine($"{"Total bruto:",-20}{FormatCurrency(grossTotal),12}");
            WriteLine($"{"Desc. productos:",-20}{$"-{FormatCurrency(itemDiscountTotal)}",12}");
        }
        WriteLine($"{"Base gravable:",-20}{FormatCurrency(sale.TaxableBaseTotal().Amount),12}");
        WriteLine($"{"IVA incluido:",-20}{FormatCurrency(sale.TaxTotal().Amount),12}");
        Write(EscPosCommands.BoldOn);
        WriteLine($"{"TOTAL:",-20}{FormatCurrency(sale.Total().Amount),12}");
        Write(EscPosCommands.BoldOff);

        var loyaltyDiscount = sale.Payments
            .Where(p => p.Method == PaymentMethod.LoyaltyPoints)
            .Sum(p => p.Amount.Amount);
        if (loyaltyDiscount > 0)
            WriteLine($"{"Desc. puntos:",-20}{$"-{FormatCurrency(loyaltyDiscount)}",12}");

        var cashPayment = sale.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Cash);
        if (cashPayment is not null)
        {
            var received = cashPayment.ReceivedAmount?.Amount ?? cashPayment.Amount.Amount;
            var change = cashPayment.ChangeAmount().Amount;
            WriteLine($"{"Efectivo:",-20}{FormatCurrency(received),12}");
            if (change > 0)
                WriteLine($"{"Cambio:",-20}{FormatCurrency(change),12}");
        }

        var methods = sale.Payments
            .Select(p => p.Method switch
            {
                PaymentMethod.Cash => "Efectivo",
                PaymentMethod.DebitCard => "Tarjeta Débito",
                PaymentMethod.CreditCard => "Tarjeta Crédito",
                PaymentMethod.Transfer => "Transferencia",
                PaymentMethod.LoyaltyPoints => "Puntos de lealtad",
                _ => p.Method.ToString()
            })
            .Distinct();
        WriteLine();
        WriteLine($"Método: {string.Join(", ", methods)}");

        WriteLine();
        Separator();

        // ── Footer ───────────────────────────────────────────────────────────
        Write(EscPosCommands.CenterAlign);
        WriteLine();
        WriteLine("Acumula y canjea puntos de lealtad");
        WriteLine("en cada compra.");
        WriteLine();
        WriteLine("¡Gracias por su compra!");
        WriteLine("Conserve su recibo.");
        WriteLine("Generado en EPOS");

        // ponytail: ~10 blank feeds ≈ 4cm at the printer's default ~4mm line spacing — leaves
        // room for the customer's verification strip. Tune the count if your printer's default
        // line spacing differs.
        for (int i = 0; i < 10; i++)
            WriteLine();

        Write(EscPosCommands.FullCut);

        return [.. buf];
    }

    private static string FormatCurrency(decimal amount) =>
        $"${amount.ToString("N0", CopCulture)}";

    private static string PresentationLabel(ProductPresentation presentation, int unitsPerPresentation) =>
        presentation == ProductPresentation.Box ? $"Caja x{unitsPerPresentation}" : "Unidad";

    private static async Task PrintViaProcessAsync(ThermalPrinterTarget target, Guid saleId, CancellationToken ct)
    {
        string command = target.Command;
        if (Path.IsPathRooted(command) && !File.Exists(command))
        {
            throw new InvalidOperationException(
                "El comando de impresión configurado no existe. Revisa la ruta de la impresora en Ajustes.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
            psi.WorkingDirectory = target.WorkingDirectory;

        psi.ArgumentList.Add(saleId.ToString());

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar el comando de impresión configurado.");

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "La impresora reportó un error. La venta queda guardada y puedes reintentar la impresión.");
        }
    }
}
