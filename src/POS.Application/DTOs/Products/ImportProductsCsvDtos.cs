namespace POS.Application.DTOs.Products;

public sealed record ImportProductsCsvRequest(string CsvContent);

public sealed record ImportProductsCsvResult(
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);
