using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Persistence port for CashClosing snapshots.
/// </summary>
public interface ICashClosingRepository
{
    Task AddAsync(CashClosing closing, CancellationToken cancellationToken = default);
}
