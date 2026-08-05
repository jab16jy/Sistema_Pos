using POS.Application.DTOs.Quotes;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class GetCustomerPayableQuotesUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IProductRepository? _products;

    public GetCustomerPayableQuotesUseCase(IQuoteRepository quotes, IProductRepository? products = null)
    {
        _quotes = quotes;
        _products = products;
    }

    public async Task<IReadOnlyList<CustomerPayableQuote>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty)
        {
            return [];
        }

        IReadOnlyList<Domain.Entities.Quote> quotes = await _quotes.GetOpenByCustomerIdAsync(customerId, ct).ConfigureAwait(false);
        var colombianToday = DateTime.UtcNow.AddHours(-5).Date;

        Domain.Entities.Quote[] payable = quotes
            .Where(q => (q.Status is QuoteStatus.Draft or QuoteStatus.Active) && q.ExpiresAt.AddHours(-5).Date >= colombianToday)
            .OrderByDescending(q => q.CreatedAt)
            .ToArray();

        Dictionary<Guid, decimal> taxRates = await ResolveTaxRatesAsync(payable, ct).ConfigureAwait(false);

        return payable
            .Select(q => new CustomerPayableQuote(
                q.Id,
                q.QuoteNumber,
                q.CustomerName,
                q.Total,
                q.ExpiresAt,
                q.Items.Select(i => new CustomerPayableQuoteItem(
                    i.ProductId,
                    i.ProductName,
                    i.Quantity.Value,
                    i.UnitPrice.Amount,
                    i.Discount,
                    i.Subtotal().Amount,
                    taxRates.GetValueOrDefault(i.ProductId))).ToArray()))
            .ToArray();
    }

    /// <summary>
    /// A quote stores no tax rate: converting it to a sale reads the product's rate at confirmation
    /// time, so the cashier preview must show the current rate too. Products that no longer resolve
    /// (deactivated since the quote was issued) fall back to zero — the sale recomputes it anyway.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ResolveTaxRatesAsync(
        IReadOnlyCollection<Domain.Entities.Quote> quotes,
        CancellationToken ct)
    {
        var rates = new Dictionary<Guid, decimal>();
        if (_products is null)
        {
            return rates;
        }

        foreach (Guid productId in quotes.SelectMany(q => q.Items).Select(i => i.ProductId).Distinct())
        {
            Domain.Entities.Product? product = await _products.GetByIdAsync(productId, ct).ConfigureAwait(false);
            if (product is not null)
            {
                rates[productId] = product.TaxRate;
            }
        }

        return rates;
    }
}
