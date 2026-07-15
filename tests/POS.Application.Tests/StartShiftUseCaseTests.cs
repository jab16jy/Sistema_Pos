using POS.Application.DTOs.Shifts;
using POS.Application.UseCases.Shifts;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class StartShiftUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithRequestedTimestamp_PreservesItNormalizedAsUtc()
    {
        var repository = new CapturingShiftRepository();
        var useCase = new StartShiftUseCase(repository, new NoOpUnitOfWork());
        var requestedAt = DateTimeOffset.UtcNow.AddHours(-2).ToOffset(TimeSpan.FromHours(-5));

        var result = await useCase.ExecuteAsync(new StartShiftRequest(
            Guid.NewGuid(),
            "cajero",
            100m,
            null,
            requestedAt), TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedShift);
        Assert.Equal(requestedAt.ToUniversalTime(), repository.AddedShift!.OpenedAt);
        Assert.Equal(requestedAt.ToUniversalTime(), result.OpenedAt);
        Assert.Equal(TimeSpan.Zero, repository.AddedShift.OpenedAt.Offset);
    }

    [Fact]
    public async Task ExecuteAsync_WithFutureTimestamp_RejectsItUsingUtcComparison()
    {
        var repository = new CapturingShiftRepository();
        var useCase = new StartShiftUseCase(repository, new NoOpUnitOfWork());
        var futureAt = DateTimeOffset.UtcNow.AddMinutes(10).ToOffset(TimeSpan.FromHours(-11));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new StartShiftRequest(Guid.NewGuid(), "cajero", 100m, null, futureAt),
            TestContext.Current.CancellationToken));

        Assert.Contains("futuro", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(repository.AddedShift);
    }

    private sealed class CapturingShiftRepository : ICashierShiftRepository
    {
        public CashierShift? AddedShift { get; private set; }

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(AddedShift);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>(AddedShift is { } shift ? [shift] : []);

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
        {
            AddedShift = shift;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
