namespace POS.Application.DTOs.BarcodeLabels;

/// <summary>Barcode symbology. Application layer resolves this based on content rules.</summary>
public enum BarcodeSymbology
{
    Code128 = 1,
    Ean13 = 2
}

/// <summary>Request to print a single product label (57x32mm).</summary>
public sealed record LabelRequest(
    string ProductName,
    decimal Price,
    string BarcodeValue,
    BarcodeSymbology Symbology = BarcodeSymbology.Code128);

/// <summary>Result of a barcode generation operation.</summary>
public sealed record BarcodeResult(
    byte[] PngBytes,
    BarcodeSymbology Symbology);
