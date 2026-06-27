using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.BarcodeLabels;

/// <summary>
/// Generates a barcode image for the given value.
/// Symbology is resolved automatically: EAN-13 for exactly 12 numeric digits, Code128 otherwise.
/// </summary>
public sealed class GenerateBarcodeUseCase
{
    private readonly IBarcodeGenerationService _barcodeService;

    public GenerateBarcodeUseCase(IBarcodeGenerationService barcodeService)
    {
        _barcodeService = barcodeService;
    }

    public async Task<BarcodeResult> ExecuteAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Barcode value is required.", nameof(value));
        }

        var symbology = ResolveSymbology(value.Trim());
        var pngBytes = await _barcodeService.GenerateAutoAsync(value.Trim(), cancellationToken).ConfigureAwait(false);

        return new BarcodeResult(pngBytes, symbology);
    }

    /// <summary>
    /// Resolves the barcode symbology based on content rules (R-BL-02):
    /// EAN-13 iff value is exactly 12 numeric digits; Code128 otherwise.
    /// </summary>
    public static BarcodeSymbology ResolveSymbology(string value)
    {
        if (value.Length == 12 && value.All(char.IsAsciiDigit))
        {
            return BarcodeSymbology.Ean13;
        }

        return BarcodeSymbology.Code128;
    }
}
