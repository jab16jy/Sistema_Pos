using POS.Application.DTOs.Quotes;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class GetCustomerPayableQuotesUseCase
{
    private readonly IQuoteRepository _quotes;

    public GetCustomerPayableQuotesUseCase(IQuoteRepository quotes)
    {
        _quotes = quotes;
    }

    public async Task<IReadOnlyList<CustomerPayableQuote>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty)
        {
            return [];
        }

        IReadOnlyList<Domain.Entities.Quote> quotes = await _quotes.GetOpenByCustomerIdAsync(customerId, ct).ConfigureAwait(false);
        var colombianToday = DateTime.UtcNow.AddHours(-5).Date;

        return quotes
            .Where(q => (q.Status is QuoteStatus.Draft or QuoteStatus.Active) && q.ExpiresAt.AddHours(-5).Date >= colombianToday)
            .OrderByDescending(q => q.CreatedAt)
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
                    i.Subtotal().Amount)).ToArray()))
            .ToArray();
    }
}
