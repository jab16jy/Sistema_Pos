using POS.Application.DTOs.BarcodeLabels;

namespace POS.Application.Interfaces;

/// <summary>
/// Port for label printing. Renders 57x32mm QuestPDF labels: product name + barcode + price.
/// Errors are surfaced to the caller — callers should handle and allow retry.
/// </summary>
public interface ILabelPrintService
{
    /// <summary>Prints one or more labels, each repeated <paramref name="copies"/> times.</summary>
    Task PrintAsync(IEnumerable<LabelRequest> labels, int copies = 1, CancellationToken cancellationToken = default);
}
