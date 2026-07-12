using POS.Application.DTOs.Quotes;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class GetQuoteHistoryUseCase
{
    private readonly IQuoteRepository _quotes;

    public GetQuoteHistoryUseCase(IQuoteRepository quotes)
    {
        _quotes = quotes;
    }

    public async Task<QuoteHistoryResult> ExecuteAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerName,
        QuoteStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, total) = await _quotes.GetHistoryAsync(from, to, customerName, status, page, pageSize, ct);

        var summaries = items.Select(q => new QuoteSummary(
            q.Id,
            q.QuoteNumber,
            q.CustomerName,
            string.IsNullOrWhiteSpace(q.CustomerEmail) ? null : q.CustomerEmail,
            string.IsNullOrWhiteSpace(q.CustomerPhone) ? null : q.CustomerPhone,
            q.Total,
            q.Status,
            q.ExpiresAt.ToString("yyyy-MM-dd"),
            q.CustomerId)).ToList();

        return new QuoteHistoryResult(summaries, total);
    }
}
