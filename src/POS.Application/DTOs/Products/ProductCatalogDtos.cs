namespace POS.Application.DTOs.Products;

public sealed record UpdateProductRequest(
    Guid ProductId,
    string Name,
    Guid CategoryId,
    decimal? CostPrice,
    decimal SalePrice,
    decimal MinimumStock,
    string? Description,
    string? Barcode,
    decimal TaxRate = 19m);

public sealed record ProductResult(
    Guid Id,
    string Name,
    Guid CategoryId,
    decimal? CostPrice,
    decimal SalePrice,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal TaxRate,
    string? Description,
    string? Barcode,
    bool IsActive);

public sealed record SearchProductsRequest(
    string? SearchText,
    bool IncludeInactive = false,
    Guid? CategoryId = null);

public sealed record ProductSearchResult(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    decimal? CostPrice,
    decimal SalePrice,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal TaxRate,
    string? Description,
    string? Barcode,
    bool IsActive);
