using POS.Application.DTOs.BarcodeLabels;

namespace POS.Application.Interfaces;

/// <summary>
/// Port for barcode image generation. Implementation must not leak ZXing types to Application layer.
/// </summary>
public interface IBarcodeGenerationService
{
    /// <summary>Generates a barcode image with an explicitly specified symbology. Returns PNG bytes.</summary>
    Task<byte[]> GenerateAsync(string value, BarcodeSymbology symbology, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a barcode image with automatically resolved symbology:
    /// EAN-13 for exactly 12 numeric digits (the 13th check digit is computed automatically),
    /// Code128 for everything else.
    /// Returns PNG bytes.
    /// </summary>
    Task<byte[]> GenerateAutoAsync(string value, CancellationToken cancellationToken = default);
}
