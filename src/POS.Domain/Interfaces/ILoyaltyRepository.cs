using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ILoyaltyRepository
{
    /// <summary>Returns the loyalty account for the given customer, or null if none exists.</summary>
    Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new loyalty account.</summary>
    Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default);

    /// <summary>Returns the customer's account, creating it atomically when missing.</summary>
    async Task<LoyaltyAccount> GetOrCreateAccountAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        LoyaltyAccount? existing = await GetAccountByCustomerIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = new LoyaltyAccount(customerId);
        await AddAccountAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    /// <summary>Persists a new loyalty transaction (earn or redeem).</summary>
    Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Returns every persisted loyalty movement linked to a sale, including compensations.</summary>
    Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

    /// <summary>Returns the most recent transactions for an account, ordered by date descending.</summary>
    Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default);

    /// <summary>Returns the currently active loyalty rule, or null if none is configured.</summary>
    Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all active/inactive rules, newest first.</summary>
    Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default);

    /// <summary>Persists a new loyalty rule. The caller must deactivate the previous rule before calling this.</summary>
    Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing loyalty rule (e.g., to deactivate it).</summary>
    Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default);

    /// <summary>Returns the computed balance for a loyalty account (SUM of signed Points).</summary>
    Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default);

    Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default);

    Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default);

    /// <summary>Returns recent program movements enriched with the customer, invoice, and responsible cashier when available.</summary>
    async Task<IReadOnlyList<LoyaltyProgramTransactionDetails>> GetRecentProgramTransactionDetailsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LoyaltyTransaction> transactions = await GetRecentProgramTransactionsAsync(maxCount, cancellationToken).ConfigureAwait(false);
        return transactions
            .Select(transaction => new LoyaltyProgramTransactionDetails(transaction, null, null, null))
            .ToList();
    }

    Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LoyaltyTier>>([]);

    Task<LoyaltyTier?> GetTierByIdAsync(Guid tierId, CancellationToken cancellationToken = default)
        => Task.FromResult<LoyaltyTier?>(null);

    Task AddTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty tier persistence is not implemented."));

    Task UpdateTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty tier persistence is not implemented."));

    Task DeleteTierAsync(Guid tierId, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty tier persistence is not implemented."));

    Task<bool> HasBenefitsRequiringTierAsync(Guid tierId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    // Benefit catalogue and claims follow the same default-implementation shape as the tier CRUD
    // above: every test file defines its own private in-memory ILoyaltyRepository fake, and without
    // defaults each of them would have to implement all of this just to keep compiling.

    Task<IReadOnlyList<LoyaltyBenefit>> GetBenefitsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LoyaltyBenefit>>([]);

    Task<LoyaltyBenefit?> GetBenefitByIdAsync(Guid benefitId, CancellationToken cancellationToken = default)
        => Task.FromResult<LoyaltyBenefit?>(null);

    Task AddBenefitAsync(LoyaltyBenefit benefit, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty benefit persistence is not implemented."));

    Task UpdateBenefitAsync(LoyaltyBenefit benefit, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty benefit persistence is not implemented."));

    Task DeleteBenefitAsync(Guid benefitId, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty benefit persistence is not implemented."));

    /// <summary>True when the benefit has already been claimed, so it must be deactivated instead of deleted.</summary>
    Task<bool> HasClaimsForBenefitAsync(Guid benefitId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    Task AddClaimAsync(LoyaltyBenefitClaim claim, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty benefit claim persistence is not implemented."));

    Task UpdateClaimAsync(LoyaltyBenefitClaim claim, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Loyalty benefit claim persistence is not implemented."));

    Task<LoyaltyBenefitClaim?> GetClaimByIdAsync(Guid claimId, CancellationToken cancellationToken = default)
        => Task.FromResult<LoyaltyBenefitClaim?>(null);

    Task<IReadOnlyList<LoyaltyBenefitClaim>> GetClaimsByAccountAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LoyaltyBenefitClaim>>([]);

    /// <summary>Loads the movement a claim cancellation must compensate from.</summary>
    Task<LoyaltyTransaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => Task.FromResult<LoyaltyTransaction?>(null);
}

/// <summary>Program-history projection that keeps persistence joins outside Presentation.</summary>
public sealed record LoyaltyProgramTransactionDetails(
    LoyaltyTransaction Transaction,
    string? CustomerName,
    string? InvoiceNumber,
    string? ResponsibleUser);
