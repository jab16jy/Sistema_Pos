using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Services;

public sealed class LabelPrintService : ILabelPrintService
{
    private readonly IBarcodeGenerationService _barcodeService;
    private readonly IPrintService _printService;

    public LabelPrintService(IBarcodeGenerationService barcodeService, IPrintService printService)
    {
        _barcodeService = barcodeService ?? throw new ArgumentNullException(nameof(barcodeService));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
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
            var pdfBytes = await BuildLabelsAsync(labelList, copies, cancellationToken).ConfigureAwait(false);

            // Write PDF to temp file and print via existing print service pattern
            var tempPath = Path.Combine(Path.GetTempPath(), $"labels_{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(tempPath, pdfBytes, cancellationToken).ConfigureAwait(false);

            try
            {
                // Use system default printer to print the PDF
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                    Verb = "print"
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process is not null)
                {
                    // Give the print spooler time to pick up the file
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                // Clean up temp file after a delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10_000).ConfigureAwait(false);
                    try { File.Delete(tempPath); } catch { /* best effort */ }
                });
            }
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

    private async Task<byte[]> BuildLabelsAsync(
        IReadOnlyList<LabelRequest> labels, int copies, CancellationToken cancellationToken)
    {
        // Pre-generate all barcode images
        var barcodeImages = new Dictionary<string, byte[]>();
        foreach (var label in labels)
        {
            if (!string.IsNullOrWhiteSpace(label.BarcodeValue) && !barcodeImages.ContainsKey(label.BarcodeValue))
            {
                var png = await _barcodeService.GenerateAutoAsync(label.BarcodeValue, cancellationToken).ConfigureAwait(false);
                barcodeImages[label.BarcodeValue] = png;
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // 57x32mm label size (1mm ≈ 2.83465 points)
                page.Size(new PageSize(
                    57 * 2.83465f,
                    32 * 2.83465f));
                page.MarginHorizontal(2);
                page.MarginVertical(1);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Content().Column(column =>
                {
                    foreach (var label in labels)
                    {
                        for (int i = 0; i < copies; i++)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text(label.ProductName)
                                        .FontSize(8).Bold();

                                    col.Item().AlignCenter().Element(e =>
                                    {
                                        if (barcodeImages.TryGetValue(label.BarcodeValue, out var png))
                                        {
                                            e.Image(png);
                                        }
                                        else
                                        {
                                            e.Text(label.BarcodeValue).FontSize(6);
                                        }
                                    });

                                    col.Item().AlignCenter().Text($"${label.Price:N0}")
                                        .FontSize(9).Bold();
                                });
                            });
                        }
                    }
                });
            });
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }
}
