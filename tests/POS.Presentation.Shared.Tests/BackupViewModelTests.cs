using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class BackupViewModelTests
{
    [Fact]
    public async Task BackupAsync_WhenBackupSucceeds_UpdatesLastSuccessfulBackupMetadata()
    {
        var settings = new BusinessSettings(
            "Store", null, null, null, null, null, null, null, false, null, null, null, null, "C:/Backups", "COP");
        var repository = new StubMutableSettingsRepository(settings);
        var viewModel = new BackupViewModel(
            new LoadSettingsUseCase(repository, new StubOperatorSession()),
            new TriggerManualBackupUseCase(repository, new StubBackupService(), new StubUnitOfWork(), new StubOperatorSession()));

        await viewModel.BackupAsync(TestContext.Current.CancellationToken);

        Assert.Equal("2026-06-27 10:30:00 UTC", viewModel.LastBackupTimestampUtc);
        Assert.Equal("1024 bytes", viewModel.LastBackupSize);
        Assert.Equal("C:/Backups/backup.sqlite", viewModel.LastBackupLocation);
        Assert.Equal("maria", viewModel.LastBackupOperator);
        Assert.Contains("validado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreAsync_WhenRestoreUnavailable_ReportsUnavailableAndReturnsFalse()
    {
        var repository = new StubMutableSettingsRepository(null);
        var viewModel = new BackupViewModel(
            new LoadSettingsUseCase(repository, new StubOperatorSession()),
            new TriggerManualBackupUseCase(repository, new StubBackupService(), new StubUnitOfWork(), new StubOperatorSession()));

        bool restored = await viewModel.RestoreAsync("C:/backup.db", TestContext.Current.CancellationToken);

        Assert.False(restored);
        Assert.False(viewModel.CanRestore);
        Assert.Contains("no está disponible", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubMutableSettingsRepository : ISettingsRepository
    {
        private BusinessSettings? _settings;

        public StubMutableSettingsRepository(BusinessSettings? settings) => _settings = settings;

        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubBackupService : IManualBackupService
    {
        public Task<BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupFile("C:/Backups/backup.sqlite", new DateTimeOffset(2026, 6, 27, 10, 30, 0, TimeSpan.Zero), 1024));

        public Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.NewGuid();
        public string Username => "maria";
        public Guid RoleId => POS.Domain.Authorization.RoleConstants.AdministratorRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.AdministratorRoleName;
        public IReadOnlySet<string> Permissions => new HashSet<string>(POS.Domain.Authorization.PermissionKeys.All);
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }
}
