using System.Globalization;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace POS.Infrastructure.Services;

public sealed class LabelPrintService : ILabelPrintService
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly IPrinterSettingsResolver _printerSettingsResolver;
    private readonly IThermalPrinterTransport _transport;

    public LabelPrintService(
        IPrinterSettingsResolver printerSettingsResolver,
        IThermalPrinterTransport transport)
    {
        _printerSettingsResolver = printerSettingsResolver ?? throw new ArgumentNullException(nameof(printerSettingsResolver));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task PrintAsync(IEnumerable<LabelRequest> labels, int copies = 1, CancellationToken cancellationToken = default)
    {
        var labelList = labels?.ToList() ?? throw new ArgumentNullException(nameof(labels));

        if (labelList.Count == 0)
        {
            throw new ApplicationException("No labels to print.");
        }

        try
        {
            ThermalPrinterTarget target = await _printerSettingsResolver.ResolveRequiredAsync(cancellationToken).ConfigureAwait(false);
            if (!target.SupportsDirectEscPos)
            {
                throw new InvalidOperationException(
                    "La configuración actual de impresora no admite impresión térmica directa de etiquetas. Usa COM, IP:PUERTO o USB.");
            }

            byte[] payload = BuildLabels(labelList, copies);
            await _transport.SendAsync(target, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to print labels: {ex.Message}", ex);
        }
    }

    private static byte[] BuildLabels(IReadOnlyList<LabelRequest> labels, int copies)
    {
        List<byte> buffer = [];
        void Write(params byte[] bytes) => buffer.AddRange(bytes);
        void WriteText(string text) => buffer.AddRange(EscPosCommands.Encode(text));
        void WriteLine(string text = "") => WriteText(text + "\n");

        Write(EscPosCommands.Init);
        Write(EscPosCommands.SelectCodePageWpc1252);
        Write([0x1B, 0x32]);

        foreach (LabelRequest label in labels)
        {
            for (int copyIndex = 0; copyIndex < copies; copyIndex++)
            {
                Write(EscPosCommands.CenterAlign);
                Write(EscPosCommands.BoldOn);
                foreach (string line in WrapText(label.ProductName, 24).Take(2))
                {
                    WriteLine(line.ToUpperInvariant());
                }
                Write(EscPosCommands.BoldOff);

                byte[] raster = BuildBarcodeRaster(label.BarcodeValue, label.Symbology);
                Write(raster);

                Write(EscPosCommands.CenterAlign);
                WriteLine(label.BarcodeValue);
                if (label.Price > 0)
                {
                    Write(EscPosCommands.BoldOn);
                    WriteLine($"${label.Price.ToString("N0", ColombianCulture)}");
                    Write(EscPosCommands.BoldOff);
                }

                Write(EscPosCommands.LeftAlign);
                WriteLine();
                WriteLine();
            }
        }

        Write(EscPosCommands.FullCut);
        return [.. buffer];
    }

    private static byte[] BuildBarcodeRaster(string value, BarcodeSymbology symbology)
    {
        BarcodeFormat format = symbology == BarcodeSymbology.Ean13
            ? BarcodeFormat.EAN_13
            : BarcodeFormat.CODE_128;

        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = 280,
                Height = 90,
                Margin = 0,
                PureBarcode = true
            }
        };

        PixelData pixelData = writer.Write(value);
        int widthBytes = (pixelData.Width + 7) / 8;
        byte[] rasterBytes = new byte[widthBytes * pixelData.Height];

        for (int y = 0; y < pixelData.Height; y++)
        {
            for (int x = 0; x < pixelData.Width; x++)
            {
                int pixelIndex = (y * pixelData.Width + x) * 4;
                byte blue = pixelData.Pixels[pixelIndex];
                byte green = pixelData.Pixels[pixelIndex + 1];
                byte red = pixelData.Pixels[pixelIndex + 2];
                byte alpha = pixelData.Pixels[pixelIndex + 3];
                bool isBlack = alpha > 0 && ((red + green + blue) / 3) < 180;

                if (!isBlack)
                {
                    continue;
                }

                int byteIndex = y * widthBytes + (x / 8);
                rasterBytes[byteIndex] |= (byte)(0x80 >> (x % 8));
            }
        }

        byte[] command = EscPosCommands.BuildRasterImageCommand(rasterBytes, pixelData.Width, pixelData.Height);
        return [.. command, 0x0A];
    }

    private static IEnumerable<string> WrapText(string value, int maxLineLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return "PRODUCTO";
            yield break;
        }

        string remaining = value.Trim();
        while (remaining.Length > maxLineLength)
        {
            int splitAt = remaining.LastIndexOf(' ', maxLineLength);
            if (splitAt <= 0)
            {
                splitAt = maxLineLength;
            }

            yield return remaining[..splitAt].Trim();
            remaining = remaining[splitAt..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }
}
