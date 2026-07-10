using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.BarcodeLabels;

/// <summary>
/// Generates a barcode image for the given value.
/// Symbology is resolved automatically: valid EAN-13 values use EAN-13, all other valid values use Code 128.
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

        var barcode = new Barcode(value);
        var symbology = ResolveSymbology(barcode.Value);
        var pngBytes = await _barcodeService.GenerateAsync(barcode.Value, symbology, cancellationToken).ConfigureAwait(false);

        return new BarcodeResult(pngBytes, symbology);
    }

    /// <summary>
    /// Resolves the barcode symbology from an already validated barcode value.
    /// </summary>
    public static BarcodeSymbology ResolveSymbology(string value)
    {
        if (Barcode.IsValidEan13(value))
        {
            return BarcodeSymbology.Ean13;
        }

        return BarcodeSymbology.Code128;
    }
}
