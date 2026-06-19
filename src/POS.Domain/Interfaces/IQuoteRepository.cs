using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Domain.Interfaces;

public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default);
    Task AddAsync(Quote quote, CancellationToken ct = default);
    Task UpdateAsync(Quote quote, CancellationToken ct = default);
    Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerName,
        QuoteStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
