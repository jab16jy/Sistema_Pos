using POS.Application.DTOs.Audit;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class AuditLogViewModelTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task LoadAsync_WhenUseCaseFails_SetsRecoverableMessage()
    {
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(new ThrowingAuditLogRepository(), FakeAdminUserRepository()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar auditoría", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithSelectedFilters_PassesFilterToUseCase()
    {
        var repository = new CapturingAuditLogRepository();
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(repository, FakeAdminUserRepository()));
        viewModel.UsernameFilter = " admin ";
        viewModel.SelectedActionFilter = "Modified";
        viewModel.SelectedEntityFilter = "User";

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(repository.LastFilter);
        Assert.Equal("admin", repository.LastFilter.Username);
        Assert.Equal("Modified", repository.LastFilter.Action);
        Assert.Equal("User", repository.LastFilter.EntityName);
    }

    [Fact]
    public async Task LoadAsync_MapsAuditRowsToSpanishReadableLabels()
    {
        var repository = new CapturingAuditLogRepository(
        [
            new AuditLog(Guid.NewGuid(), "admin", "Added", "Customer", Guid.NewGuid().ToString(), "Registro creado."),
            new AuditLog(Guid.NewGuid(), "admin", "Modified", "Quote", "COT-1", "Campos modificados: Status.")
        ]);
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(repository, FakeAdminUserRepository()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.Logs.Count);
        Assert.Equal("Creado", viewModel.Logs[0].ActionLabel);
        Assert.Equal("Cliente", viewModel.Logs[0].EntityLabel);
        Assert.Contains("Registro creado", viewModel.Logs[0].Details);
        Assert.Equal("Actualizado", viewModel.Logs[1].ActionLabel);
        Assert.Equal("Cotización", viewModel.Logs[1].EntityLabel);
    }

    [Fact]
    public async Task LoadAsync_HidesRawGuidIdsButKeepsBusinessReferences()
    {
        var repository = new CapturingAuditLogRepository(
        [
            new AuditLog(Guid.NewGuid(), "admin", "Deleted", "Product", Guid.NewGuid().ToString(), "Se eliminó: Coca-Cola."),
            new AuditLog(Guid.NewGuid(), "admin", "Added", "Sale", "FAC-001", "Registro creado.")
        ]);
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(repository, FakeAdminUserRepository()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        // Raw GUID is useless to the admin -> hidden; the invoice number is a real reference -> shown.
        Assert.DoesNotContain("Ref.", viewModel.Logs[0].Details);
        Assert.Contains("Coca-Cola", viewModel.Logs[0].Details);
        Assert.Contains("FAC-001", viewModel.Logs[1].Details);
    }

    [Fact]
    public async Task LoadAsync_WithSpanishFilters_MapsThemToRawAuditValues()
    {
        var repository = new CapturingAuditLogRepository();
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(repository, FakeAdminUserRepository()));
        viewModel.SelectedActionFilter = "Creado";
        viewModel.SelectedEntityFilter = "Cliente";

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(repository.LastFilter);
        Assert.Equal("Added", repository.LastFilter.Action);
        Assert.Equal("Customer", repository.LastFilter.EntityName);
    }

    [Fact]
    public async Task LoadAsync_WhenRequestingUserIsNotAdministrator_SetsRecoverableMessage()
    {
        var repository = new CapturingAuditLogRepository();
        var cashierId = Guid.NewGuid();
        var users = new FakeUserRepository();
        users.Add(new User("cajero", "hash", UserRole.Cashier), cashierId);
        var viewModel = CreateViewModel(new GetAuditLogsUseCase(repository, users), cashierId);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar auditoría", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AuditLogViewModel CreateViewModel(GetAuditLogsUseCase useCase, Guid? requestingUserId = null)
        => new(useCase, new FakeOperatorSession(requestingUserId ?? AdminId));

    private static FakeUserRepository FakeAdminUserRepository()
    {
        var users = new FakeUserRepository();
        users.Add(new User("admin", "hash", UserRole.Administrator), AdminId);
        return users;
    }

    private sealed class ThrowingAuditLogRepository : IAuditLogRepository
    {
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int limit = 100,
            AuditLogFilter? filter = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CapturingAuditLogRepository : IAuditLogRepository
    {
        private readonly IReadOnlyList<AuditLog> _logs;

        public CapturingAuditLogRepository(IReadOnlyList<AuditLog>? logs = null)
        {
            _logs = logs ?? [];
        }

        public AuditLogFilter? LastFilter { get; private set; }

        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int limit = 100,
            AuditLogFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(_logs);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _usersById = [];

        public void Add(User user, Guid id) => _usersById[id] = user;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.GetValueOrDefault(id));

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(u => u.Username == username));

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_usersById.Count > 0);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>([.. _usersById.Values]);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeOperatorSession(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "admin";
        public UserRole Role => UserRole.Administrator;
        public bool IsAuthenticated => OperatorId != Guid.Empty;
    }
}
