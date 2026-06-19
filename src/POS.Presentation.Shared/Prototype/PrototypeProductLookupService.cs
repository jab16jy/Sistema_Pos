using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Presentation.Shared.Prototype;

public sealed class PrototypeProductLookupService : IProductLookupService
{
    private static readonly IReadOnlyList<ProductLookupResult> Products =
    [
        new(Guid.Parse("1f4ac51b-7d30-43c1-a8a6-c64082f7ad7f"), "Arroz 500g", "7701000000011", 2500m, 24m),
        new(Guid.Parse("15f0e87b-4c51-48ea-9a21-2fe0ef7d8d73"), "Aceite 1L", "7701000000028", 9800m, 12m),
        new(Guid.Parse("51d3a0c0-4025-4c23-89ae-30a95e32d80f"), "Panela 500g", "7701000000035", 4200m, 8m),
        new(Guid.Parse("cb1dd489-d7e6-4bd2-a82a-03041ef50ee3"), "Café molido 250g", "7701000000042", 11200m, 6m),
        new(Guid.Parse("c95cf550-455e-4934-a989-23f40d36c211"), "Leche entera 1L", "7701000000059", 3900m, 18m)
    ];

    public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(Products.Take(5).ToArray() as IReadOnlyList<ProductLookupResult>);
        }

        string normalizedQuery = query.Trim();
        ProductLookupResult[] matches = Products
            .Where(product =>
                product.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                product.Barcode.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToArray();

        return Task.FromResult(matches as IReadOnlyList<ProductLookupResult>);
    }
}
