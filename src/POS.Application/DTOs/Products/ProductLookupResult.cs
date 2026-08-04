namespace POS.Application.DTOs.Products;

public sealed record ProductLookupResult(
    Guid ProductId,
    string Name,
    string Barcode,
    decimal UnitPrice,
    decimal AvailableStock,
    decimal MinimumStock = 0m,
    IReadOnlyList<ProductPresentationDto>? Presentations = null);
