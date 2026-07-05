using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public enum ThermalPrinterTargetKind
{
    Serial,
    Tcp,
    Usb,
    Command
}

public sealed record ThermalPrinterTarget(
    string Command,
    string? WorkingDirectory,
    ThermalPrinterTargetKind Kind)
{
    public bool SupportsDirectEscPos => Kind is ThermalPrinterTargetKind.Serial or ThermalPrinterTargetKind.Tcp or ThermalPrinterTargetKind.Usb;
}

public interface IPrinterSettingsResolver
{
    Task<ThermalPrinterTarget> ResolveRequiredAsync(CancellationToken cancellationToken = default);
}

public interface IThermalPrinterTransport
{
    Task SendAsync(ThermalPrinterTarget target, byte[] payload, CancellationToken cancellationToken = default);
}

public sealed partial class PrinterSettingsResolver : IPrinterSettingsResolver
{
    private readonly PrinterRuntimeOptions _runtimeOptions;
    private readonly AppDbContext _dbContext;

    public PrinterSettingsResolver(PrinterRuntimeOptions runtimeOptions, AppDbContext dbContext)
    {
        _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ThermalPrinterTarget> ResolveRequiredAsync(CancellationToken cancellationToken = default)
    {
        BusinessSettings? settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? command = FirstNonEmpty(settings?.PrinterCommand, _runtimeOptions.Command);
        string? workingDirectory = FirstNonEmpty(settings?.PrinterWorkingDirectory, _runtimeOptions.WorkingDirectory);

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException(
                "La impresora no está configurada. La venta queda guardada y podrás imprimir el recibo cuando configures la impresora.");
        }

        return new ThermalPrinterTarget(command.Trim(), workingDirectory?.Trim(), Classify(command));
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static ThermalPrinterTargetKind Classify(string command)
    {
        string trimmed = command.Trim();
        if (SerialPortRegex().IsMatch(trimmed))
        {
            return ThermalPrinterTargetKind.Serial;
        }

        if (TcpAddressRegex().IsMatch(trimmed))
        {
            return ThermalPrinterTargetKind.Tcp;
        }

        if (trimmed.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(@"\\.\USB", StringComparison.OrdinalIgnoreCase))
        {
            return ThermalPrinterTargetKind.Usb;
        }

        return ThermalPrinterTargetKind.Command;
    }

    [GeneratedRegex(@"^COM\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex SerialPortRegex();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}:\d{2,5}$")]
    private static partial Regex TcpAddressRegex();
}

public sealed class ThermalPrinterTransport : IThermalPrinterTransport
{
    public async Task SendAsync(ThermalPrinterTarget target, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);

        switch (target.Kind)
        {
            case ThermalPrinterTargetKind.Serial:
                await SendViaSerialAsync(target.Command, payload, cancellationToken).ConfigureAwait(false);
                return;
            case ThermalPrinterTargetKind.Tcp:
                await SendViaTcpAsync(target.Command, payload, cancellationToken).ConfigureAwait(false);
                return;
            case ThermalPrinterTargetKind.Usb:
                await SendViaFileStreamAsync(NormalizeUsbPath(target.Command), payload, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new InvalidOperationException("La configuración actual de impresora no admite escritura ESC/POS directa.");
        }
    }

    private static string NormalizeUsbPath(string command)
    {
        string trimmed = command.Trim();
        if (trimmed.StartsWith(@"\\.\USB", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        string suffix = new string(trimmed.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(suffix)
            ? @"\\.\USB001"
            : $@"\\.\{suffix.ToUpperInvariant()}";
    }

    private static async Task SendViaSerialAsync(string port, byte[] data, CancellationToken ct)
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

    private static async Task SendViaTcpAsync(string address, byte[] data, CancellationToken ct)
    {
        string[] parts = address.Split(':');
        string host = parts[0];
        int port = int.Parse(parts[1]);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
        await tcp.GetStream().WriteAsync(data, ct).ConfigureAwait(false);
        await tcp.GetStream().FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task SendViaFileStreamAsync(string path, byte[] data, CancellationToken ct)
        => await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
}
