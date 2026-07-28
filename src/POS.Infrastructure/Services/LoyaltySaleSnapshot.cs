using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

/// <summary>
/// The loyalty figures a printed document shows for one sale: points earned by that sale, the
/// customer's current balance, and the tier that balance qualifies for.
/// </summary>
/// <remarks>
/// Shared by the PDF invoice and the thermal receipt so both print the same numbers from the same
/// query. The tier is resolved from the balance, NOT from <c>Customer.LoyaltyTierId</c> — that
/// column is never written outside tests, so the by-FK lookup the printers used before this type
/// always returned null and the tier line never printed. Balance-derived tiers also match what
/// AccumulateLoyaltyPointsUseCase and GetLoyaltyBalanceUseCase already do.
/// </remarks>
internal sealed record LoyaltySaleSnapshot(int PointsEarned, int Balance, LoyaltyTier? Tier)
{
    public static readonly LoyaltySaleSnapshot Empty = new(0, 0, null);

    /// <summary>True when there is any loyalty figure worth printing.</summary>
    public bool HasContent => PointsEarned != 0 || Balance != 0 || Tier is not null;

    public static async Task<LoyaltySaleSnapshot> LoadAsync(
        AppDbContext dbContext, Guid? customerId, Guid saleId, CancellationToken cancellationToken)
    {
        if (customerId is not Guid id)
        {
            return Empty;
        }

        LoyaltyAccount? account = await dbContext.LoyaltyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CustomerId == id, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return Empty;
        }

        // Points are signed, so summing gives the balance directly (there is no stored balance column).
        int balance = await dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyAccountId == account.Id)
            .SumAsync(t => (int?)t.Points, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        // Earned plus EarnReversed, so a receipt reprinted after a partial return shows the points
        // the sale actually left the customer with, not the original pre-return amount.
        int pointsEarned = await dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.SaleId == saleId
                && t.LoyaltyAccountId == account.Id
                && (t.Type == LoyaltyTransactionType.Earned || t.Type == LoyaltyTransactionType.EarnReversed))
            .SumAsync(t => (int?)t.Points, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        // Filtered and ordered in memory on purpose: MinPoints is persisted as TEXT, so SQLite can
        // neither compare nor ORDER BY it as a decimal. Tier counts are a handful of rows.
        List<LoyaltyTier> tiers = await dbContext.LoyaltyTiers
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        LoyaltyTier? tier = tiers
            .Where(t => t.MinPoints <= balance)
            .OrderByDescending(t => t.MinPoints)
            .FirstOrDefault();

        return new LoyaltySaleSnapshot(pointsEarned, balance, tier);
    }
}
