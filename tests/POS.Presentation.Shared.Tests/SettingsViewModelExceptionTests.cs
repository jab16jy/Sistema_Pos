using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class SettingsViewModelExceptionTests
{
    [Fact]
    public async Task LoadCommand_WhenLoadFails_SetsRecoverableMessage()
    {
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(new ThrowingSettingsRepository(), new NoOpOperatorSession()),
            new SaveSettingsUseCase(new ThrowingSettingsRepository(), new NoOpUnitOfWork(), new NoOpOperatorSession()));

        viewModel.LoadCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar configuración", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingSettingsRepository : ISettingsRepository
    {
        public Task<POS.Domain.Entities.BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromException<POS.Domain.Entities.BusinessSettings?>(new InvalidOperationException("SQLite bloqueado"));

        public Task SaveAsync(POS.Domain.Entities.BusinessSettings settings, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("SQLite bloqueado"));
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class NoOpBackupService : IManualBackupService
    {
        public Task<POS.Application.DTOs.Settings.BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(new POS.Application.DTOs.Settings.BackupFile(destinationDirectory + "\\backup.sqlite", DateTimeOffset.UtcNow, 1));

        public Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpOperatorSession : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.Empty;
        public string Username => string.Empty;
        public Guid RoleId => POS.Domain.Authorization.RoleConstants.CashierRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.CashierRoleName;
        public IReadOnlySet<string> Permissions => new HashSet<string>();
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => false;
    }
}
