using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ILoyaltyRepository
{
    /// <summary>Returns the loyalty account for the given customer, or null if none exists.</summary>
    Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new loyalty account.</summary>
    Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default);

    /// <summary>Persists a new loyalty transaction (earn or redeem).</summary>
    Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent transactions for an account, ordered by date descending.</summary>
    Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default);

    /// <summary>Returns the currently active loyalty rule, or null if none is configured.</summary>
    Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a new loyalty rule. The caller must deactivate the previous rule before calling this.</summary>
    Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing loyalty rule (e.g., to deactivate it).</summary>
    Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default);

    /// <summary>Returns the computed balance for a loyalty account (SUM of signed Points).</summary>
    Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default);
}
