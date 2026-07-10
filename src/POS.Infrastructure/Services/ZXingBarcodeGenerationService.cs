using System.IO.Compression;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using POS.Domain.ValueObjects;
using ZXing;
using ZXing.Common;

namespace POS.Infrastructure.Services;

public sealed class ZXingBarcodeGenerationService : IBarcodeGenerationService
{
    public Task<byte[]> GenerateAsync(string value, BarcodeSymbology symbology, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationException("Barcode value cannot be empty.");
        }

        try
        {
            var format = symbology == BarcodeSymbology.Ean13
                ? BarcodeFormat.EAN_13
                : BarcodeFormat.CODE_128;

            return Task.FromResult(Encode(value, format));
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to generate barcode: {ex.Message}", ex);
        }
    }

    public Task<byte[]> GenerateAutoAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationException("Barcode value cannot be empty.");
        }

        try
        {
            var barcode = new Barcode(value);
            var format = ResolveSymbology(barcode.Value);
            return Task.FromResult(Encode(barcode.Value, format));
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to generate barcode: {ex.Message}", ex);
        }
    }

    private static BarcodeFormat ResolveSymbology(string value)
    {
        if (Barcode.IsValidEan13(value))
        {
            return BarcodeFormat.EAN_13;
        }

        return BarcodeFormat.CODE_128;
    }

    private static byte[] Encode(string value, BarcodeFormat format)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = 300,
                Height = 120,
                Margin = 4,
                PureBarcode = true
            }
        };

        var pixelData = writer.Write(value);
        return ConvertToPng(pixelData.Pixels, pixelData.Width, pixelData.Height);
    }

    /// <summary>
    /// Converts BGRA pixel data to a PNG byte array using a minimal PNG encoder.
    /// </summary>
    private static byte[] ConvertToPng(byte[] pixels, int width, int height)
    {
        using var ms = new MemoryStream();

        // PNG signature
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR chunk
        WriteChunk(ms, "IHDR", writer =>
        {
            WriteInt32BigEndian(writer, width);
            WriteInt32BigEndian(writer, height);
            writer.WriteByte(8);  // bit depth
            writer.WriteByte(6);  // color type: RGBA
            writer.WriteByte(0);  // compression method
            writer.WriteByte(0);  // filter method
            writer.WriteByte(0);  // interlace method
        });

        // IDAT chunk - image data
        WriteChunk(ms, "IDAT", writer =>
        {
            using var deflateStream = new DeflateStream(writer, CompressionLevel.Fastest, leaveOpen: true);

            for (int y = 0; y < height; y++)
            {
                deflateStream.WriteByte(0); // filter: None
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    // BGRA → RGBA
                    deflateStream.WriteByte(pixels[offset + 2]); // R
                    deflateStream.WriteByte(pixels[offset + 1]); // G
                    deflateStream.WriteByte(pixels[offset]);     // B
                    deflateStream.WriteByte(pixels[offset + 3]); // A
                }
            }
        });

        // IEND chunk
        WriteChunk(ms, "IEND", _ => { });

        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, string type, Action<Stream> writeData)
    {
        using var dataStream = new MemoryStream();
        writeData(dataStream);
        var data = dataStream.ToArray();

        // Length
        WriteInt32BigEndian(ms, data.Length);

        // Type
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        ms.Write(typeBytes);

        // Data
        ms.Write(data);

        // CRC (over type + data)
        var crcData = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcData, 0);
        data.CopyTo(crcData, typeBytes.Length);
        var crc = Crc32.Compute(crcData);
        WriteInt32BigEndian(ms, (int)crc);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    /// <summary>Minimal CRC-32 implementation for PNG chunks.</summary>
    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
                table[i] = crc;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}
