using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    private static readonly byte[] CmdInit        = [0x1B, 0x40];
    private static readonly byte[] CmdCenterAlign = [0x1B, 0x61, 0x01];
    private static readonly byte[] CmdLeftAlign   = [0x1B, 0x61, 0x00];
    private static readonly byte[] CmdBoldOn      = [0x1B, 0x45, 0x01];
    private static readonly byte[] CmdBoldOff     = [0x1B, 0x45, 0x00];
    private static readonly byte[] CmdDoubleSize  = [0x1D, 0x21, 0x11];
    private static readonly byte[] CmdNormalSize  = [0x1D, 0x21, 0x00];
    private static readonly byte[] CmdFullCut     = [0x1D, 0x56, 0x00];

    private static readonly CultureInfo CopCulture = CultureInfo.GetCultureInfo("es-CO");

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

        var receiptBytes = BuildReceiptBytes(sale, settings);
        ThermalPrinterTarget target = await _printerSettingsResolver.ResolveRequiredAsync(cancellationToken).ConfigureAwait(false);

        if (target.SupportsDirectEscPos)
        {
            await _transport.SendAsync(target, receiptBytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        await PrintViaProcessAsync(target, saleId, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildReceiptBytes(Sale sale, BusinessSettings? settings)
    {
        var buf = new List<byte>();
        void Write(byte[] b) => buf.AddRange(b);
        void WriteText(string text) => buf.AddRange(Encoding.UTF8.GetBytes(text));
        void WriteLine(string text = "") => WriteText(text + "\n");
        void Separator() => WriteLine("--------------------------------");

        Write(CmdInit);

        // ── Header ──────────────────────────────────────────────────────────
        Write(CmdCenterAlign);
        Write(CmdBoldOn);
        Write(CmdDoubleSize);
        WriteLine(settings?.BusinessName ?? "POS Local");
        Write(CmdNormalSize);
        Write(CmdBoldOff);

        if (!string.IsNullOrWhiteSpace(settings?.BusinessAddress))
            WriteLine(settings.BusinessAddress);
        if (!string.IsNullOrWhiteSpace(settings?.TaxId))
            WriteLine($"NIT: {settings.TaxId}");

        WriteLine();

        // ── Invoice metadata ─────────────────────────────────────────────────
        Write(CmdLeftAlign);
        Separator();
        WriteLine($"Factura: {sale.InvoiceNumber}");
        WriteLine($"Fecha:   {sale.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        Separator();

        // ── Line items ───────────────────────────────────────────────────────
        foreach (var item in sale.Items)
        {
            var name = item.ProductName.Length > 20
                ? item.ProductName[..20]
                : item.ProductName;

            var qty      = $"x{item.Quantity.ToString("0.##", CopCulture)}";
            var subtotal = FormatCurrency(item.Subtotal().Amount);

            // 32-char line: name(20) qty(5) price(7)
            var line = $"{name,-20}{qty,5} {subtotal,6}";
            WriteLine(line);
        }

        Separator();

        // ── Totals ───────────────────────────────────────────────────────────
        WriteLine($"{"Base gravable:",-20}{FormatCurrency(sale.TaxableBaseTotal().Amount),12}");
        WriteLine($"{"IVA incluido:",-20}{FormatCurrency(sale.TaxTotal().Amount),12}");
        Write(CmdBoldOn);
        WriteLine($"{"TOTAL:",-20}{FormatCurrency(sale.Total().Amount),12}");
        Write(CmdBoldOff);

        var cashPayment = sale.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Cash);
        if (cashPayment is not null)
        {
            var received = cashPayment.ReceivedAmount?.Amount ?? cashPayment.Amount.Amount;
            var change   = cashPayment.ChangeAmount().Amount;
            WriteLine($"{"Efectivo:",-20}{FormatCurrency(received),12}");
            if (change > 0)
                WriteLine($"{"Cambio:",-20}{FormatCurrency(change),12}");
        }

        var methods = sale.Payments
            .Select(p => p.Method switch
            {
                PaymentMethod.Cash       => "Efectivo",
                PaymentMethod.DebitCard  => "Tarjeta Débito",
                PaymentMethod.CreditCard => "Tarjeta Crédito",
                PaymentMethod.Transfer   => "Transferencia",
                PaymentMethod.LoyaltyPoints => "Puntos de lealtad",
                _                        => p.Method.ToString()
            })
            .Distinct();
        WriteLine($"Método: {string.Join(", ", methods)}");

        Separator();

        // ── Footer ───────────────────────────────────────────────────────────
        Write(CmdCenterAlign);
        WriteLine();
        WriteLine("¡Gracias por su compra!");
        WriteLine("Conserve su recibo.");
        WriteLine();
        WriteLine();
        WriteLine();
        Write(CmdFullCut);

        return [.. buf];
    }

    private static string FormatCurrency(decimal amount) =>
        $"${amount.ToString("N0", CopCulture)}";

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
