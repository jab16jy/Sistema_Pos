namespace POS.Application.Interfaces;

/// <summary>
/// Lists the printers installed in the operating system so the user can pick one by name
/// instead of typing it. Printing by name routes through the OS spooler, so the physical
/// port (USB001, etc.) is resolved automatically and can change without reconfiguring.
/// </summary>
public interface IInstalledPrinterProvider
{
    IReadOnlyList<string> GetInstalledPrinters();
}
