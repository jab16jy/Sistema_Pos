using POS.Application.DTOs.Receivables;

namespace POS.Application.Interfaces;

/// <summary>
/// Read-model query service for customer receivables.
/// Returns denormalized summaries for display purposes.
/// </summary>
public interface IReceivableQueryService
{
    Task<IReadOnlyList<ReceivableSummary>> GetOpenReceivablesAsync(
        Guid? customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
