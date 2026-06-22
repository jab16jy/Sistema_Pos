using POS.Domain.Entities;

namespace POS.Application.DTOs.Purchases;

public sealed record SaveSupplierRequest(
    Guid? Id,
    string Name,
    string TaxId,
    string? Phone,
    string? Email,
    bool IsActive);

public sealed record SupplierResult(
    Guid Id,
    string Name,
    string TaxId,
    string? Phone,
    string? Email,
    bool IsActive)
{
    public static SupplierResult FromSupplier(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.TaxId,
        supplier.Phone,
        supplier.Email,
        supplier.IsActive);
}

public sealed record SupplierSearchRequest(string? SearchText, bool IncludeInactive = false);

public sealed record SupplierSearchResult(
    Guid Id,
    string Name,
    string TaxId,
    string? Phone,
    string? Email,
    bool IsActive)
{
    public static SupplierSearchResult FromSupplier(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.TaxId,
        supplier.Phone,
        supplier.Email,
        supplier.IsActive);
}
