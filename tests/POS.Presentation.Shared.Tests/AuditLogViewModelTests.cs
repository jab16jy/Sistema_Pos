using POS.Application.DTOs.Audit;
using POS.Application.UseCases.Audit;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
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

    [Fact]
    public async Task LoadAsync_WithSelectedFilters_PassesFilterToUseCase()
    {
        var repository = new CapturingAuditLogRepository();
        var viewModel = new AuditLogViewModel(new GetAuditLogsUseCase(repository))
        {
            UsernameFilter = " admin ",
            SelectedActionFilter = "Modified",
            SelectedEntityFilter = "User"
        };

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(repository.LastFilter);
        Assert.Equal("admin", repository.LastFilter.Username);
        Assert.Equal("Modified", repository.LastFilter.Action);
        Assert.Equal("User", repository.LastFilter.EntityName);
    }

    private sealed class ThrowingAuditLogRepository : IAuditLogRepository
    {
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int limit = 100,
            AuditLogFilter? filter = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CapturingAuditLogRepository : IAuditLogRepository
    {
        public AuditLogFilter? LastFilter { get; private set; }

        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int limit = 100,
            AuditLogFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult<IReadOnlyList<AuditLog>>([]);
        }
    }
}
