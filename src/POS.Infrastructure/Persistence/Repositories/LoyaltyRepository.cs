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
        // SQLite can't ORDER BY DateTimeOffset — sort client-side like the sibling methods below.
        List<LoyaltyTransaction> transactions = await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyAccountId == loyaltyAccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxCount)
            .ToList();
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
        LoyaltyRule? tracked = _dbContext.LoyaltyRules.Local.FirstOrDefault(existing => existing.Id == rule.Id);
        if (tracked is not null && !ReferenceEquals(tracked, rule))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(rule);
        }
        else
        {
            _dbContext.LoyaltyRules.Update(rule);
        }
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

    public async Task<IReadOnlyList<LoyaltyProgramTransactionDetails>> GetRecentProgramTransactionDetailsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
    {
        List<LoyaltyTransaction> transactions = await _dbContext.LoyaltyTransactions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Guid> customerIdsByAccount = (await _dbContext.LoyaltyAccounts
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(account => account.Id, account => account.CustomerId);

        Dictionary<Guid, string> customerNames = (await _dbContext.Customers
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(customer => customer.Id, customer => customer.FullName);

        Dictionary<Guid, Sale> sales = (await _dbContext.Sales
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(sale => sale.Id);

        Dictionary<Guid, string> usernames = (await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(user => user.Id, user => user.Username);

        return transactions
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(maxCount)
            .Select(transaction =>
            {
                customerIdsByAccount.TryGetValue(transaction.LoyaltyAccountId, out Guid customerId);
                customerNames.TryGetValue(customerId, out string? customerName);
                Sale? sale = transaction.SaleId is { } saleId && sales.TryGetValue(saleId, out Sale? foundSale) ? foundSale : null;
                string? responsibleUser = sale is not null && usernames.TryGetValue(sale.CashierUserId, out string? username) ? username : null;
                return new LoyaltyProgramTransactionDetails(transaction, customerName, sale?.InvoiceNumber, responsibleUser);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
        // SQLite can't ORDER BY decimal — sort client-side.
        => (await _dbContext.LoyaltyTiers
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderBy(tier => tier.MinPoints)
            .ToList();

    public Task<LoyaltyTier?> GetTierByIdAsync(Guid tierId, CancellationToken cancellationToken = default)
        => _dbContext.LoyaltyTiers.FirstOrDefaultAsync(tier => tier.Id == tierId, cancellationToken);

    public async Task AddTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
        => await _dbContext.LoyaltyTiers.AddAsync(tier, cancellationToken).ConfigureAwait(false);

    public Task UpdateTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
    {
        LoyaltyTier? tracked = _dbContext.LoyaltyTiers.Local.FirstOrDefault(existing => existing.Id == tier.Id);
        if (tracked is not null && !ReferenceEquals(tracked, tier))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(tier);
        }
        else
        {
            _dbContext.LoyaltyTiers.Update(tier);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteTierAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        LoyaltyTier? tier = _dbContext.LoyaltyTiers.Local.FirstOrDefault(existing => existing.Id == tierId)
            ?? await _dbContext.LoyaltyTiers.FirstOrDefaultAsync(existing => existing.Id == tierId, cancellationToken).ConfigureAwait(false);
        if (tier is not null)
        {
            _dbContext.LoyaltyTiers.Remove(tier);
        }
    }
}
