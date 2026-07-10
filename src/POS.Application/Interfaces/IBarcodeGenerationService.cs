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
    /// EAN-13 for a valid 13-digit value with its stored check digit,
    /// Code128 for other valid Code 128 values.
    /// Returns PNG bytes.
    /// </summary>
    Task<byte[]> GenerateAutoAsync(string value, CancellationToken cancellationToken = default);
}
