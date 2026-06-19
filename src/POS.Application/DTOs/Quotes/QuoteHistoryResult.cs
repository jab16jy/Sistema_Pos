namespace POS.Application.DTOs.Quotes;

public sealed record QuoteHistoryResult(
    IReadOnlyList<QuoteSummary> Items,
    int TotalCount);
