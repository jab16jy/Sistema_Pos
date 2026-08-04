namespace POS.Application.DTOs.Products;

public sealed record CreateProductRequest(
    string Name,
    Guid CategoryId,
    decimal? CostPrice,
    decimal SalePrice,
    decimal InitialStock,
    decimal MinimumStock,
    string? Description,
    string? Barcode,
    decimal TaxRate = 19m,
    IReadOnlyList<ProductPresentationDto>? Presentations = null,
    Guid OperatorUserId = default);
