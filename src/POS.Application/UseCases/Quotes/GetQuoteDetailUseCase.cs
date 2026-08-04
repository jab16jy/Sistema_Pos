using POS.Application.DTOs.Quotes;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class GetQuoteDetailUseCase
{
    private readonly IQuoteRepository _quotes;

    public GetQuoteDetailUseCase(IQuoteRepository quotes)
    {
        _quotes = quotes;
    }

    public async Task<QuoteDetailResult?> ExecuteAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await _quotes.GetByIdAsync(quoteId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
        {
            return null;
        }

        var items = quote.Items
            .Select(i => new QuoteDetailItemResult(
                i.ProductId,
                i.ProductName,
                i.Quantity.Value,
                i.UnitPrice.Amount,
                i.Discount,
                i.Subtotal().Amount,
                i.PresentationName,
                i.UnitsPerPresentation))
            .ToList();

        return new QuoteDetailResult(
            quote.Id,
            quote.QuoteNumber,
            quote.CustomerId,
            quote.CustomerName,
            quote.CustomerEmail,
            quote.CustomerPhone,
            quote.Status,
            quote.ExpiresAt.ToString("yyyy-MM-dd"),
            quote.Total,
            items);
    }
}
