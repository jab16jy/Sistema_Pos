using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

/// <summary>
/// ESC/POS thermal printer service.
/// Auto-detects connection type from POS_PRINTER_COMMAND:
///   COM3 (or COM\d+)    → SerialPort at 9600 baud
///   192.168.1.100:9100  → TcpClient
///   USB / \\.\USB…      → FileStream to \\.\USB001
///   anything else       → legacy Process.Start fallback (same as PrintService)
/// Falls back gracefully when not configured.
/// </summary>
public sealed partial class EscPosPrinterService : IPrintService
{
    private static readonly byte[] CmdInit       = [0x1B, 0x40];
    private static readonly byte[] CmdCenterAlign = [0x1B, 0x61, 0x01];
    private static readonly byte[] CmdLeftAlign   = [0x1B, 0x61, 0x00];
    private static readonly byte[] CmdBoldOn      = [0x1B, 0x45, 0x01];
    private static readonly byte[] CmdBoldOff     = [0x1B, 0x45, 0x00];
    private static readonly byte[] CmdDoubleSize  = [0x1D, 0x21, 0x11];
    private static readonly byte[] CmdNormalSize  = [0x1D, 0x21, 0x00];
    private static readonly byte[] CmdFullCut     = [0x1D, 0x56, 0x00];

    private readonly PrinterRuntimeOptions _options;

    public EscPosPrinterService(PrinterRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Command);

    public async Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "La impresora no está configurada. La venta queda guardada y podrás imprimir el recibo cuando configures la impresora.");
        }

        var target = _options.Command!.Trim();

        if (SerialPortRegex().IsMatch(target))
        {
            await PrintViaSerialAsync(target, saleId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (TcpAddressRegex().IsMatch(target))
        {
            await PrintViaTcpAsync(target, saleId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (target.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(@"\\.\USB", StringComparison.OrdinalIgnoreCase))
        {
            await PrintViaFileStreamAsync(@"\\.\USB001", saleId, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Legacy fallback: external process (same behaviour as original PrintService)
        await PrintViaProcessAsync(target, saleId, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildReceiptBytes(Guid saleId)
    {
        var buf = new List<byte>();
        void Write(byte[] b) => buf.AddRange(b);
        void WriteText(string text) => buf.AddRange(Encoding.UTF8.GetBytes(text));

        Write(CmdInit);
        Write(CmdCenterAlign);
        Write(CmdBoldOn);
        Write(CmdDoubleSize);
        WriteText("Esthetic Medical Store\n");
        Write(CmdNormalSize);
        Write(CmdBoldOff);
        WriteText($"Venta: {saleId}\n");
        WriteText($"{DateTime.Now:yyyy-MM-dd HH:mm}\n");
        WriteText("--------------------------------\n");
        Write(CmdLeftAlign);
        WriteText("Recibo generado correctamente.\n");
        WriteText("Gracias por su compra!\n");
        WriteText("\n\n\n");
        Write(CmdFullCut);

        return [.. buf];
    }

    private static async Task PrintViaSerialAsync(string port, Guid saleId, CancellationToken ct)
    {
        using var serial = new SerialPort(port, 9600, Parity.None, 8, StopBits.One)
        {
            WriteTimeout = 5000,
            ReadTimeout = 2000
        };
        serial.Open();
        var data = BuildReceiptBytes(saleId);
        await serial.BaseStream.WriteAsync(data, ct).ConfigureAwait(false);
        await serial.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task PrintViaTcpAsync(string address, Guid saleId, CancellationToken ct)
    {
        var parts = address.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
        var data = BuildReceiptBytes(saleId);
        await tcp.GetStream().WriteAsync(data, ct).ConfigureAwait(false);
        await tcp.GetStream().FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task PrintViaFileStreamAsync(string path, Guid saleId, CancellationToken ct)
    {
        var data = BuildReceiptBytes(saleId);
        await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
    }

    private async Task PrintViaProcessAsync(string command, Guid saleId, CancellationToken ct)
    {
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

        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            psi.WorkingDirectory = _options.WorkingDirectory;

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

    [GeneratedRegex(@"^COM\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex SerialPortRegex();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}:\d{2,5}$")]
    private static partial Regex TcpAddressRegex();
}
