using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
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
}
