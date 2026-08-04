namespace POS.Application.DTOs.Products;

public sealed record ProductLookupResult(
    Guid ProductId,
    string Name,
    string Barcode,
    decimal UnitPrice,
    decimal AvailableStock,
    decimal MinimumStock = 0m,
    int? UnitsPerBox = null,
    decimal? BoxPrice = null,
    int? HalfBoxUnits = null,
    decimal? HalfBoxPrice = null,
    int? QuarterBoxUnits = null,
    decimal? QuarterBoxPrice = null);
