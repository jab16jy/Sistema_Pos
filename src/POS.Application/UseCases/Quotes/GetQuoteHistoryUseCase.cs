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
        CancellationToken ct = default) =>
        await ExecuteAsync(from, to, customerName, status, page, pageSize, DateTime.UtcNow, ct).ConfigureAwait(false);

    /// <summary>
    /// <see cref="ExecuteAsync(DateOnly?, DateOnly?, string?, QuoteStatus?, int, int, CancellationToken)"/>
    /// with the clock supplied, so the calendar-expiry derivation is deterministic under test.
    /// </summary>
    public async Task<QuoteHistoryResult> ExecuteAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerName,
        QuoteStatus? status,
        int page,
        int pageSize,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var (items, total) = await _quotes.GetHistoryAsync(from, to, customerName, status, page, pageSize, ct);

        var summaries = new List<QuoteSummary>();
        DateOnly colombianToday = DateOnly.FromDateTime(utcNow.AddHours(ColombiaUtcOffsetHours));
        foreach (var quote in items)
        {
            bool hasSuccessfulEmail = _sendLogs is not null && quote.Status != QuoteStatus.Converted &&
                (await _sendLogs.GetByDocumentAsync(quote.Id, ct).ConfigureAwait(false))
                .Any(log => log.DocumentType == DocumentType.Quote && log.Channel == SendChannel.Email && log.Success);

            // Expiry is a property of the quote's own deadline, never of whether an email went out.
            // Gating this on HasSuccessfulEmail — as this used to — meant a lapsed quote that was never
            // emailed reported no expiry at all, so it kept its edit affordance and carried no
            // "Renueva o crea otra cotización" guidance. Send history is a channel fact; a deadline
            // elapsing is a calendar fact, and the two must not be entangled.
            //
            // The expiration date is the quote's own Colombian calendar day, matching the UTC-5 rule
            // ConvertQuoteToSaleUseCase and Quote.CanEditOn apply. This previously used ExpiresAt's raw
            // UTC date, which disagrees with those two for every quote in the evening UTC-5 hours.
            DateOnly expiration = quote.ColombiaExpirationDate;

            // A terminal status already says what happened to the quote, so a "Vencida" chip on a
            // converted or cancelled quote would be noise — and it must not make such a row look
            // lapsed, since that feeds CanConvertToSale. Open proposals get the full label set.
            bool isOpenProposal = quote.Status is QuoteStatus.Draft or QuoteStatus.Active;
            string? reminder = isOpenProposal ? ResolveReminderLabel(expiration, colombianToday) : null;
            bool isExpiredByCalendar = isOpenProposal && expiration < colombianToday;

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
                reminder,
                isExpiredByCalendar));
        }

        return new QuoteHistoryResult(summaries, total);
    }

    /// <summary>Same fixed offset as the aggregate's expiry rule; see <see cref="Quote.ColombiaExpirationDate"/>.</summary>
    private const int ColombiaUtcOffsetHours = -5;

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
