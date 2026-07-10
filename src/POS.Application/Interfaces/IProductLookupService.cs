using POS.Application.DTOs.Products;

namespace POS.Application.Interfaces;

public interface IProductLookupService
{
    Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<ProductLookupResult?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        => Task.FromResult<ProductLookupResult?>(null);
}
