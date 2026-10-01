using POS.Application.DTOs.Quotes;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Quotes;

/// <summary>
/// Resolves requested quote lines into immutable <see cref="QuoteItem"/> snapshots. Creation and
/// editing both go through here on purpose: this is a money path, and a drifted second copy of the
/// price/presentation/units resolution is exactly how a quote ends up totalling something other
/// than the invoice it becomes.
/// </summary>
internal static class QuoteLineBuilder
{
    public static async Task<IReadOnlyList<QuoteItem>> BuildAllAsync(
        IReadOnlyCollection<CreateQuoteItemRequest> requestedItems,
        IProductRepository products,
        CancellationToken ct)
    {
        var items = new List<QuoteItem>(requestedItems.Count);

        foreach (var item in requestedItems)
        {
            var product = await products.GetByIdAsync(item.ProductId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Producto {item.ProductId} no encontrado.");

            // The product resolves units and price for whichever presentation it offers, so a
            // quote can sell every pack the shop configured, not just the box it used to know.
            items.Add(new QuoteItem(
                product.Id,
                product.Name,
                product.PriceFor(item.PresentationName),
                new Quantity(item.Quantity),
                item.Discount,
                item.PresentationName,
                product.UnitsFor(item.PresentationName)));
        }

        return items;
    }
}