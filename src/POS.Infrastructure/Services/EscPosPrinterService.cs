using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

/// <summary>
/// ESC/POS thermal printer service.
/// Auto-detects connection type from POS_PRINTER_COMMAND:
///   A Windows-installed printer name → winspool.drv RAW print (OpenPrinter/WritePrinter)
///   COM3 (or COM\d+)    → SerialPort at 9600 baud
///   192.168.1.100:9100  → TcpClient
///   USB / \\.\USB…      → FileStream to the raw USB print device path
///   anything else       → legacy Process.Start fallback (same as PrintService)
/// Falls back gracefully when not configured.
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

    /// <remarks>
    /// Only reflects the POS_PRINTER_COMMAND env var, not the BusinessSettings row saved from the
    /// Configuración screen — <see cref="PrintReceiptAsync"/> resolves the effective command itself.
    /// </remarks>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Command);

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

        // The printer command set from the Configuración screen (persisted in BusinessSettings)
        // takes precedence over the POS_PRINTER_COMMAND env var used for dev/CI defaults.
        var command = !string.IsNullOrWhiteSpace(settings?.PrinterCommand)
            ? settings.PrinterCommand
            : _options.Command;

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException(
                "La impresora no está configurada. La venta queda guardada y podrás imprimir el recibo cuando configures la impresora.");
        }

        var receiptBytes = BuildReceiptBytes(sale, settings);
        var target = command.Trim();

        // Raw USB device paths (\\.\USB001) are known to throw access-denied on Windows because the
        // print class driver stack usually owns the handle exclusively. When the printer is installed
        // in Windows (Impresoras y escáneres) — which is how USB thermal printers are normally set up —
        // sending RAW data through the spooler by printer name is the reliable path, so it's tried first.
        if (TryPrintViaWindowsPrinter(target, receiptBytes))
        {
            return;
        }

        if (target.SupportsDirectEscPos)
        {
            await PrintViaSerialAsync(target, receiptBytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (TcpAddressRegex().IsMatch(target))
        {
            await PrintViaTcpAsync(target, receiptBytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (target.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(@"\\.\USB", StringComparison.OrdinalIgnoreCase))
        {
            string devicePath = target.StartsWith(@"\\.\", StringComparison.Ordinal)
                ? target
                : $@"\\.\{target}";
            await PrintViaFileStreamAsync(devicePath, receiptBytes, cancellationToken).ConfigureAwait(false);
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

            var qty      = $"x{item.Quantity:0.##}";
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

    private static async Task PrintViaSerialAsync(string port, byte[] data, CancellationToken ct)
    {
        using var serial = new SerialPort(port, 9600, Parity.None, 8, StopBits.One)
        {
            WriteTimeout = 5000,
            ReadTimeout = 2000
        };
        serial.Open();
        await serial.BaseStream.WriteAsync(data, ct).ConfigureAwait(false);
        await serial.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task PrintViaTcpAsync(string address, byte[] data, CancellationToken ct)
    {
        var parts = address.Split(':');
        var host  = parts[0];
        var port  = int.Parse(parts[1]);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
        await tcp.GetStream().WriteAsync(data, ct).ConfigureAwait(false);
        await tcp.GetStream().FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task PrintViaFileStreamAsync(string path, byte[] data, CancellationToken ct)
    {
        await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the raw bytes through the Windows print spooler (RAW datatype) when <paramref name="printerName"/>
    /// matches an installed printer. Returns false (without printing) when the name isn't a known printer, so
    /// the caller can try the other transports instead.
    /// </summary>
    private static bool TryPrintViaWindowsPrinter(string printerName, byte[] data)
    {
        if (!OpenPrinterW(printerName, out IntPtr hPrinter, IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var docInfo = new DOCINFOW
            {
                pDocName = "Recibo POS",
                pOutputFile = null,
                pDataType = "RAW"
            };

            if (!StartDocPrinterW(hPrinter, 1, ref docInfo))
            {
                throw new InvalidOperationException("No se pudo iniciar el trabajo de impresión en la impresora configurada.");
            }

            try
            {
                if (!StartPagePrinter(hPrinter))
                {
                    throw new InvalidOperationException("No se pudo iniciar la página de impresión.");
                }

                try
                {
                    if (!WritePrinter(hPrinter, data, data.Length, out _))
                    {
                        throw new InvalidOperationException("La impresora rechazó los datos enviados. Verifica que esté encendida y con papel.");
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFOW
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinterW(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinterW(IntPtr hPrinter, int level, ref DOCINFOW pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    private async Task PrintViaProcessAsync(string command, Guid saleId, CancellationToken ct)
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
