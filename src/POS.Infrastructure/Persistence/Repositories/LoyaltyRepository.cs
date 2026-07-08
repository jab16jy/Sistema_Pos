using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class LoyaltyRepository : ILoyaltyRepository
{
    private readonly AppDbContext _dbContext;

    public LoyaltyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);
    }

    public async Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoyaltyAccounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoyaltyTransactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(
        Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyAccountId == loyaltyAccountId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.LoyaltyRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
    {
        List<LoyaltyRule> rules = await _dbContext.LoyaltyRules
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rules
            .OrderByDescending(rule => rule.CreatedAt)
            .Take(maxCount)
            .ToList();
    }

    public async Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoyaltyRules.AddAsync(rule, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
    {
        _dbContext.LoyaltyRules.Update(rule);
        return Task.CompletedTask;
    }

    public async Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyAccountId == loyaltyAccountId)
            .SumAsync(t => t.Points, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.Points > 0)
            .SumAsync(t => t.Points, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.LoyaltyAccounts
            .AsNoTracking()
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        List<LoyaltyTransaction> transactions = await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.Type == LoyaltyTransactionType.Redeemed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return transactions.Count(t => t.CreatedAt >= from && t.CreatedAt <= to);
    }

    public async Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
    {
        List<LoyaltyTransaction> transactions = await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return transactions
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(maxCount)
            .ToList();
    }
}
