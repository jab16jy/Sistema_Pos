using POS.Application.DTOs.Loyalty;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

/// <summary>
/// Returns the current loyalty balance and last N transactions for a customer.
/// Returns null if the customer has no loyalty account.
/// </summary>
public sealed class GetLoyaltyBalanceUseCase
{
    private readonly ILoyaltyRepository _loyalty;

    public GetLoyaltyBalanceUseCase(ILoyaltyRepository loyalty)
    {
        _loyalty = loyalty;
    }

    public async Task<LoyaltyBalanceResult?> ExecuteAsync(Guid customerId, int recentTransactionCount = 20, CancellationToken cancellationToken = default)
    {
        var account = await _loyalty.GetAccountByCustomerIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        var balance = await _loyalty.GetBalanceAsync(account.Id, cancellationToken).ConfigureAwait(false);
        var transactions = await _loyalty.GetTransactionsAsync(account.Id, recentTransactionCount, cancellationToken).ConfigureAwait(false);
        var activeRule = await _loyalty.GetActiveRuleAsync(cancellationToken).ConfigureAwait(false);

        var tiers = await _loyalty.GetTiersAsync(cancellationToken).ConfigureAwait(false);
        var currentTierEntity = LoyaltyTierMapper.ResolveCurrentTier(tiers, balance);
        var currentTier = currentTierEntity is null ? null : LoyaltyTierMapper.ToResult(currentTierEntity);

        var transactionResults = transactions
            .Select(t => new LoyaltyTransactionResult(
                t.Id,
                t.Points,
                t.Type.ToString(),
                t.SaleId,
                t.Description,
                t.CreatedAt))
            .ToList()
            .AsReadOnly();

        return new LoyaltyBalanceResult(
            customerId,
            account.Id,
            balance,
            account.IsActive,
            transactionResults,
            activeRule?.RedemptionValuePerPoint ?? 0m,
            activeRule?.MinimumRedeemPoints ?? 0,
            currentTier);
    }
}
