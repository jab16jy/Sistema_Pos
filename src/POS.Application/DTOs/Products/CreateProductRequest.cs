namespace POS.Application.DTOs.Products;

public sealed record CreateProductRequest(
    string Name,
    Guid CategoryId,
    decimal? CostPrice,
    decimal SalePrice,
    decimal CurrentStock,
    decimal MinimumStock,
    string? Description,
    string? Barcode,
    decimal TaxRate = 19m,
    int? UnitsPerBox = null,
    decimal? BoxPrice = null);
