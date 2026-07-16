using POS.Application.DTOs.Shifts;
using POS.Application.UseCases.Shifts;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class ShiftClosingSummaryTests
{
    [Fact]
    public async Task CloseShift_PersistsFreshBoundedSalesSummary()
    {
        var shift = new CashierShift(Guid.NewGuid(), "cashier", new Money(100m));
        var shifts = new SingleShiftRepository(shift);
        var sales = new BoundedSummarySaleRepository(new ShiftSalesSummaryData(4_200m, 2_800m, 1_400m));

        var result = await new CloseShiftUseCase(shifts, sales, new NoOpUnitOfWork())
            .ExecuteAsync(new CloseShiftRequest(shift.Id, 2_900m, null), TestContext.Current.CancellationToken);

        Assert.True(result.HasClosingSummary);
        Assert.Equal(4_200m, result.SalesTotal);
        Assert.Equal(2_800m, result.CashPayments);
        Assert.Equal(1_400m, result.DigitalPayments);
        Assert.Equal(2_900m, result.ExpectedCash);
        Assert.NotNull(sales.ClosedAt);
        Assert.True(sales.ClosedAt > shift.OpenedAt);
    }

    private sealed class SingleShiftRepository(CashierShift shift) : ICashierShiftRepository
    {
        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(shift);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(id == shift.Id ? shift : null);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>([shift]);

        public Task AddAsync(CashierShift cashierShift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BoundedSummarySaleRepository(ShiftSalesSummaryData summary) : ISaleRepository
    {
        public DateTimeOffset? ClosedAt { get; private set; }

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));

        public Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
            Guid cashierUserId,
            DateTimeOffset openedAt,
            DateTimeOffset closedAt,
            CancellationToken cancellationToken = default)
        {
            ClosedAt = closedAt;
            return Task.FromResult(summary);
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
