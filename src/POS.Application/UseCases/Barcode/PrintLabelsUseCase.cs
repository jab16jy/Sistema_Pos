using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.BarcodeLabels;

/// <summary>
/// Prints one or more product labels via the label print service.
/// Exceptions from the print service propagate to the caller for retry handling.
/// </summary>
public sealed class PrintLabelsUseCase
{
    private readonly ILabelPrintService _labelPrintService;

    public PrintLabelsUseCase(ILabelPrintService labelPrintService)
    {
        _labelPrintService = labelPrintService;
    }

    public async Task ExecuteAsync(IReadOnlyList<LabelRequest> labels, int copies = 1, CancellationToken cancellationToken = default)
    {
        if (labels is null || labels.Count == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        if (copies < 1)
        {
            throw new ArgumentException("Copies must be at least 1.", nameof(copies));
        }

        // Exceptions intentionally propagate — caller decides on retry
        await _labelPrintService.PrintAsync(labels, copies, cancellationToken).ConfigureAwait(false);
    }
}
