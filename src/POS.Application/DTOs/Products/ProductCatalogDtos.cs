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
    decimal TaxRate = 19m,
    int? UnitsPerBox = null,
    decimal? BoxPrice = null,
    int? HalfBoxUnits = null,
    decimal? HalfBoxPrice = null,
    int? QuarterBoxUnits = null,
    decimal? QuarterBoxPrice = null);

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
    bool IsActive,
    int? UnitsPerBox = null,
    decimal? BoxPrice = null,
    int? HalfBoxUnits = null,
    decimal? HalfBoxPrice = null,
    int? QuarterBoxUnits = null,
    decimal? QuarterBoxPrice = null);

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
    bool IsActive,
    int? UnitsPerBox = null,
    decimal? BoxPrice = null,
    int? HalfBoxUnits = null,
    decimal? HalfBoxPrice = null,
    int? QuarterBoxUnits = null,
    decimal? QuarterBoxPrice = null)
{
    public bool IsOutOfStock => CurrentStock <= 0m;

    public bool IsLowStock => MinimumStock > 0m && CurrentStock > 0m && CurrentStock <= MinimumStock;
}
