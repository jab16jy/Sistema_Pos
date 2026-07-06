using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class ShiftsViewModelTests
{
    [Fact]
    public async Task LoadCommand_WhenShiftQueryFails_SetsRecoverableMessage()
    {
        var repository = new ThrowingShiftRepository();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork()),
            new CloseShiftUseCase(repository, new NoOpUnitOfWork()),
            new GetShiftsUseCase(repository),
            new StubOperatorSession());

        viewModel.LoadCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar turnos", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingShiftRepository : ICashierShiftRepository
    {
        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<CashierShift>>(new InvalidOperationException("Consulta falló"));

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username { get; } = "cajero";
        public UserRole Role { get; } = UserRole.Cashier;
        public bool IsAuthenticated => true;
    }
}
