using POS.Application.DTOs.Quotes;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class GetQuoteHistoryUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IDocumentSendLogRepository? _sendLogs;

    public GetQuoteHistoryUseCase(IQuoteRepository quotes, IDocumentSendLogRepository? sendLogs = null)
    {
        _quotes = quotes;
        _sendLogs = sendLogs;
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

        var summaries = new List<QuoteSummary>();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var quote in items)
        {
            bool hasSuccessfulEmail = _sendLogs is not null && quote.Status != QuoteStatus.Converted &&
                (await _sendLogs.GetByDocumentAsync(quote.Id, ct).ConfigureAwait(false))
                .Any(log => log.DocumentType == DocumentType.Quote && log.Channel == SendChannel.Email && log.Success);

            string? reminder = hasSuccessfulEmail ? ResolveReminderLabel(DateOnly.FromDateTime(quote.ExpiresAt), today) : null;
            summaries.Add(new QuoteSummary(
                quote.Id,
                quote.QuoteNumber,
                quote.CustomerName,
                string.IsNullOrWhiteSpace(quote.CustomerEmail) ? null : quote.CustomerEmail,
                string.IsNullOrWhiteSpace(quote.CustomerPhone) ? null : quote.CustomerPhone,
                quote.Total,
                quote.Status,
                quote.ExpiresAt.ToString("yyyy-MM-dd"),
                quote.CustomerId,
                hasSuccessfulEmail,
                reminder));
        }

        return new QuoteHistoryResult(summaries, total);
    }

    private static string? ResolveReminderLabel(DateOnly expiration, DateOnly today)
    {
        int daysRemaining = expiration.DayNumber - today.DayNumber;
        return daysRemaining switch
        {
            < 0 => "Vencida",
            0 => "Vence hoy",
            1 or 2 => "Próxima a vencer",
            _ => null
        };
    }
}
