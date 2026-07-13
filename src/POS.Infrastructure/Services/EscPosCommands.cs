using System.Text;

namespace POS.Infrastructure.Services;

/// <summary>
/// Shared ESC/POS byte commands and text encoding for thermal printers, used by both
/// <see cref="EscPosPrinterService"/> (receipts) and <see cref="LabelPrintService"/> (labels) so the
/// two never drift out of sync.
/// </summary>
internal static class EscPosCommands
{
    public static readonly byte[] Init = [0x1B, 0x40];
    // ponytail: WPC1252 (n=16) is the Epson ESC/POS code table closest to .NET's Latin1 encoding —
    // both agree on the printable Latin-1 Supplement range (áéíóúñ¿¡), which is all Spanish receipts
    // need. Confirmed against Epson's TM-T20II ESC t reference. If a printer's firmware maps n=16 to
    // something else, swap this table id and Encode together.
    public static readonly byte[] SelectCodePageWpc1252 = [0x1B, 0x74, 0x10];
    public static readonly byte[] CenterAlign = [0x1B, 0x61, 0x01];
    public static readonly byte[] LeftAlign = [0x1B, 0x61, 0x00];
    public static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];
    public static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];
    public static readonly byte[] FullCut = [0x1D, 0x56, 0x00];

    public static byte[] Encode(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>Wraps pre-packed 1bpp raster bits (MSB-first, 1=print) in a GS v 0 raster image command.</summary>
    public static byte[] BuildRasterImageCommand(byte[] packedBits, int widthDots, int heightDots)
    {
        int widthBytes = (widthDots + 7) / 8;
        List<byte> command =
        [
            0x1D, 0x76, 0x30, 0x00,
            (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
            (byte)(heightDots & 0xFF), (byte)((heightDots >> 8) & 0xFF)
        ];
        command.AddRange(packedBits);
        return [.. command];
    }
}
