using POS.Application.DTOs.Audit;
using POS.Application.UseCases.Audit;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class AuditLogViewModelTests
{
    [Fact]
    public async Task LoadAsync_WhenUseCaseFails_SetsRecoverableMessage()
    {
        var viewModel = new AuditLogViewModel(new GetAuditLogsUseCase(new ThrowingAuditLogRepository()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar auditoría", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingAuditLogRepository : IAuditLogRepository
    {
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
