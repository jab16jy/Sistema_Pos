using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class CompensateSaleLoyaltyUseCaseTests
{
    [Fact]
    public async Task CompensateFullAsync_AppendsExactTypedCompensations_AndIsIdempotent()
    {
        Sale sale = CreateSale();
        var repo = new InMemoryLoyaltyRepository();
        var accountId = Guid.NewGuid();
        repo.Rows.Add(new LoyaltyTransaction(accountId, LoyaltyTransactionType.Earned, 90, sale.Id));
        repo.Rows.Add(new LoyaltyTransaction(accountId, LoyaltyTransactionType.Redeemed, -10, sale.Id));
        var useCase = new CompensateSaleLoyaltyUseCase(repo);

        await useCase.CompensateFullAsync(sale, "Cancellation", TestContext.Current.CancellationToken);
        await useCase.CompensateFullAsync(sale, "Cancellation retry", TestContext.Current.CancellationToken);

        Assert.Collection(repo.Rows.OrderBy(row => row.CreatedAt),
            row => Assert.Equal(90, row.Points),
            row => Assert.Equal(-10, row.Points),
            row => { Assert.Equal(LoyaltyTransactionType.EarnReversed, row.Type); Assert.Equal(-90, row.Points); },
            row => { Assert.Equal(LoyaltyTransactionType.RedemptionRestored, row.Type); Assert.Equal(10, row.Points); });
        Assert.Equal(2, repo.Rows.Where(row => row.CompensatesTransactionId is not null).Select(row => row.CompensatesTransactionId).Distinct().Count());
    }

    [Fact]
    public async Task CompensatePartialAsync_UsesOriginalQuantitiesAndTenderAllocation()
    {
        Sale sale = CreateSale();
        var repo = new InMemoryLoyaltyRepository();
        var accountId = Guid.NewGuid();
        repo.Rows.Add(new LoyaltyTransaction(accountId, LoyaltyTransactionType.Earned, 90, sale.Id));
        repo.Rows.Add(new LoyaltyTransaction(accountId, LoyaltyTransactionType.Redeemed, -10, sale.Id));

        await new CompensateSaleLoyaltyUseCase(repo).CompensatePartialAsync(
            sale,
            [new CompensateSaleLoyaltyUseCase.PartialReturnLine(sale.Items.Single().Id, 2)],
            "Partial return",
            TestContext.Current.CancellationToken);

        Assert.Contains(repo.Rows, row => row.Type == LoyaltyTransactionType.EarnReversed && row.Points == -18);
        Assert.Contains(repo.Rows, row => row.Type == LoyaltyTransactionType.RedemptionRestored && row.Points == 2);
    }

    [Fact]
    public async Task CompensatePartialAsync_DuplicateSaleItemIsRejectedBeforeAnyCompensationIsStaged()
    {
        Sale sale = CreateSale();
        var repo = new InMemoryLoyaltyRepository();
        repo.Rows.Add(new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.Earned, 90, sale.Id));
        var line = new CompensateSaleLoyaltyUseCase.PartialReturnLine(sale.Items.Single().Id, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => new CompensateSaleLoyaltyUseCase(repo).CompensatePartialAsync(
            sale,
            [line, line],
            "Duplicate partial return",
            TestContext.Current.CancellationToken));

        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task CompensateReturnAsync_TwoPartialReturnsAppendOnlyTheirUnpaidCumulativeDeltas()
    {
        Sale sale = CreateSale();
        var repo = new InMemoryLoyaltyRepository();
        var accountId = Guid.NewGuid();
        LoyaltyTransaction earned = new(accountId, LoyaltyTransactionType.Earned, 90, sale.Id);
        LoyaltyTransaction redeemed = new(accountId, LoyaltyTransactionType.Redeemed, -10, sale.Id);
        repo.Rows.AddRange([earned, redeemed]);
        var useCase = new CompensateSaleLoyaltyUseCase(repo);

        SaleReturn first = CreateReturn(sale, 3);
        await useCase.CompensateReturnAsync(sale, [], first, "First partial return", TestContext.Current.CancellationToken);

        SaleReturn second = CreateReturn(sale, 7);
        await useCase.CompensateReturnAsync(sale, [first], second, "Second partial return", TestContext.Current.CancellationToken);
        await useCase.CompensateReturnAsync(sale, [first], second, "Retry", TestContext.Current.CancellationToken);

        LoyaltyTransaction[] compensations = repo.Rows.Where(row => row.CompensatesTransactionId is not null).ToArray();
        Assert.Equal(4, compensations.Length);
        Assert.Equal(-90, compensations.Where(row => row.CompensatesTransactionId == earned.Id).Sum(row => row.Points));
        Assert.Equal(10, compensations.Where(row => row.CompensatesTransactionId == redeemed.Id).Sum(row => row.Points));
        Assert.All(compensations.Where(row => row.SaleReturnId == second.Id), row => Assert.Equal(second.Id, row.SaleReturnId));
    }

    [Fact]
    public async Task CompensateReturnAsync_SubtractsHistoricalCompensationBeforeAppendingTheCurrentDelta()
    {
        Sale sale = CreateSale();
        var repo = new InMemoryLoyaltyRepository();
        LoyaltyTransaction earned = new(Guid.NewGuid(), LoyaltyTransactionType.Earned, 90, sale.Id);
        repo.Rows.Add(earned);
        SaleReturn previous = CreateReturn(sale, 3);
        repo.Rows.Add(LoyaltyTransaction.CreateCompensation(earned, "Historical", 27, previous.Id));
        SaleReturn current = CreateReturn(sale, 4);

        await new CompensateSaleLoyaltyUseCase(repo).CompensateReturnAsync(
            sale, [previous], current, "Current partial return", TestContext.Current.CancellationToken);

        LoyaltyTransaction delta = Assert.Single(repo.Rows, row => row.SaleReturnId == current.Id);
        Assert.Equal(-36, delta.Points);
        Assert.Equal(current.Id, delta.SaleReturnId);
    }

    private static Sale CreateSale()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(1m));
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(10m), new Money(0m));
        sale.AddPayment(new Payment(PaymentMethod.LoyaltyPoints, new Money(1_000m)));
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(9_000m), new Money(9_000m)));
        sale.Complete();
        return sale;
    }

    private static SaleReturn CreateReturn(Sale sale, int quantity)
    {
        SaleItem item = sale.Items.Single();
        var saleReturn = new SaleReturn(sale.Id, sale.CashierUserId, "Return", Guid.NewGuid());
        saleReturn.AddItem(new SaleReturnItem(
            saleReturn.Id, item.Id, item.ProductId, item.ProductName, quantity, item.UnitPrice, new Money(item.UnitPrice.Amount * quantity)));
        return saleReturn;
    }

    private sealed class InMemoryLoyaltyRepository : ILoyaltyRepository
    {
        public List<LoyaltyTransaction> Rows { get; } = [];

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
        {
            Rows.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Rows.Where(row => row.SaleId == saleId).ToList());

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyAccount?>(null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyRule?>(null);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
    }
}
