using System.Diagnostics;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class PrintService : IPrintService
{
    private readonly PrinterRuntimeOptions _options;

    public PrintService(PrinterRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Command))
        {
            throw new InvalidOperationException("La impresora no está configurada. La venta queda guardada y podrás imprimir el recibo cuando configures la impresora.");
        }

        if (Path.IsPathRooted(_options.Command) && !File.Exists(_options.Command))
        {
            throw new InvalidOperationException("El comando de impresión configurado no existe. Revisa la ruta de la impresora en Ajustes.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _options.Command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            psi.WorkingDirectory = _options.WorkingDirectory;
        }

        psi.ArgumentList.Add(saleId.ToString());

        using var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException("No se pudo iniciar el comando de impresión configurado.");
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("La impresora reportó un error. La venta queda guardada y puedes reintentar la impresión.");
        }
    }
}
