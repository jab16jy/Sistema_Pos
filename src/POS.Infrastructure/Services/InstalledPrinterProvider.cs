using System.Runtime.InteropServices;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

/// <summary>
/// Enumerates Windows-installed printers via winspool <c>EnumPrinters</c> (PRINTER_INFO_4, names only).
/// No extra dependency — same winspool P/Invoke style already used by <see cref="EscPosPrinterService"/>.
/// Returns an empty list off Windows or when no printer is installed.
/// </summary>
public sealed class InstalledPrinterProvider : IInstalledPrinterProvider
{
    private const uint PrinterEnumLocal = 0x00000002;
    private const uint PrinterEnumConnections = 0x00000004;
    private const uint Level = 4;

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return [];
        }

        const uint flags = PrinterEnumLocal | PrinterEnumConnections;

        // First call sizes the buffer; a zero need means no printers.
        EnumPrintersW(flags, null, Level, IntPtr.Zero, 0, out uint needed, out _);
        if (needed == 0)
        {
            return [];
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!EnumPrintersW(flags, null, Level, buffer, needed, out _, out uint count))
            {
                return [];
            }

            var names = new List<string>((int)count);
            int structSize = Marshal.SizeOf<PRINTER_INFO_4W>();
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PRINTER_INFO_4W>(buffer + i * structSize);
                if (!string.IsNullOrWhiteSpace(info.pPrinterName))
                {
                    names.Add(info.pPrinterName);
                }
            }

            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_4W
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pPrinterName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrintersW(
        uint Flags, string? Name, uint Level, IntPtr pPrinterEnum, uint cbBuf, out uint pcbNeeded, out uint pcReturned);
}
