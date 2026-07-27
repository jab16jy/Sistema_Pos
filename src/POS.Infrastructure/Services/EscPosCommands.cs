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

    /// <summary>Vertical dots per millimetre on a 203 dpi thermal head (8 dots/mm), the near-universal
    /// resolution for 58/80 mm receipt printers and the unit ESC J and ESC 3 both count in.</summary>
    public const int DotsPerMillimetre = 8;

    /// <summary>ESC 3 n — sets the line feed height to <paramref name="dots"/>, replacing the printer's
    /// ~30 dot default. Must be at least as tall as the characters in use or lines overlap.</summary>
    public static byte[] SetLineSpacing(byte dots) => [0x1B, 0x33, dots];

    /// <summary>
    /// ESC J n — feeds paper by an exact number of dots, independent of the current line spacing.
    /// Emitted in 255-dot chunks because n is a single byte. Preferred over blank lines whenever the
    /// distance matters in millimetres, since a blank line is only as tall as the spacing in effect.
    /// </summary>
    public static byte[] FeedDots(int dots)
    {
        List<byte> command = [];
        while (dots > 0)
        {
            byte chunk = (byte)Math.Min(dots, 255);
            command.AddRange([0x1B, 0x4A, chunk]);
            dots -= chunk;
        }

        return [.. command];
    }

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
