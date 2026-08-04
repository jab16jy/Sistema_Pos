using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.DTOs.Products;

/// <summary>
/// One shop-defined way of selling a product: a name, how many base units it holds, and its own
/// price. Carried in and out of the catalogue as a list, so adding a "paquete de 3" needs no new
/// field anywhere along the way.
/// </summary>
public sealed record ProductPresentationDto(string Name, int Units, decimal Price);

public static class ProductPresentationDtoExtensions
{
    /// <summary>Shape the catalogue sends into the shape <see cref="Product.ReplacePresentations"/> takes.</summary>
    public static IEnumerable<(string Name, int Units, Money Price)> ToDomain(
        this IReadOnlyList<ProductPresentationDto>? presentations) =>
        (presentations ?? []).Select(p => (p.Name, p.Units, new Money(p.Price)));

    public static IReadOnlyList<ProductPresentationDto> ToDto(this Product product) =>
        product.Presentations
               .Select(p => new ProductPresentationDto(p.Name, p.Units, p.Price.Amount))
               .ToList();
}
