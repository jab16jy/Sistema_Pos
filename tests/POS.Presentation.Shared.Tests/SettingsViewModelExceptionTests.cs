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
            new LoadSettingsUseCase(new ThrowingSettingsRepository()),
            new SaveSettingsUseCase(new ThrowingSettingsRepository(), new NoOpUnitOfWork()),
            new TriggerManualBackupUseCase(new ThrowingSettingsRepository(), new NoOpBackupService()));

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
        public Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(destinationDirectory + "\\backup.sqlite");
    }
}
