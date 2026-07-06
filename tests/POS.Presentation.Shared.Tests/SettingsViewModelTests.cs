using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void PickDefaultPrinter_SkipsVirtualPrinters_ReturnsPhysicalOne()
    {
        var printers = new[]
        {
            "Microsoft Print to PDF",
            "Microsoft XPS Document Writer",
            "Fax",
            "Jaltech POS-80C",
        };

        Assert.Equal("Jaltech POS-80C", SettingsViewModel.PickDefaultPrinter(printers));
    }

    [Fact]
    public void PickDefaultPrinter_OnlyVirtualPrinters_ReturnsNull()
    {
        var printers = new[] { "Microsoft Print to PDF", "OneNote (Desktop)", "Fax" };

        Assert.Null(SettingsViewModel.PickDefaultPrinter(printers));
    }

    [Fact]
    public void PickDefaultPrinter_EmptyList_ReturnsNull()
    {
        Assert.Null(SettingsViewModel.PickDefaultPrinter([]));
    }

    [Fact]
    public async Task LoadAsync_LoadsPersistedSmtpConfiguration()
    {
        var settings = new SettingsResult(
            Guid.NewGuid(),
            "Store",
            null,
            null,
            "smtp.office365.com",
            587,
            "ventas@store.com",
            "secret",
            true,
            "ventas@store.com",
            null,
            null,
            null,
            "COP",
            []);
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(new StubSettingsRepository(settings)),
            new SaveSettingsUseCase(new StubMutableSettingsRepository(), new StubUnitOfWork()),
            new TriggerManualBackupUseCase(new StubMutableSettingsRepository(), new StubBackupService()),
            null,
            new StubWhatsAppService());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("smtp.office365.com", viewModel.SmtpHost);
        Assert.Equal("587", viewModel.SmtpPort);
        Assert.Equal("ventas@store.com", viewModel.SmtpUser);
        Assert.Equal("secret", viewModel.SmtpPassword);
        Assert.True(viewModel.SmtpUseSsl);
        Assert.True(viewModel.IsWhatsAppReady);
    }

    private sealed class StubSettingsRepository : POS.Domain.Interfaces.ISettingsRepository
    {
        private readonly SettingsResult _settings;

        public StubSettingsRepository(SettingsResult settings)
        {
            _settings = settings;
        }

        public Task<POS.Domain.Entities.BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<POS.Domain.Entities.BusinessSettings?>(new POS.Domain.Entities.BusinessSettings(
                _settings.BusinessName,
                _settings.BusinessAddress,
                _settings.TaxId,
                _settings.SmtpHost,
                _settings.SmtpPort,
                _settings.SmtpUser,
                _settings.SmtpPassword,
                _settings.SmtpUseSsl,
                _settings.SmtpSenderAddress,
                _settings.PrinterCommand,
                _settings.PrinterWorkingDirectory,
                _settings.BackupDirectory,
                _settings.CurrencyCode));

        public Task SaveAsync(POS.Domain.Entities.BusinessSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubMutableSettingsRepository : POS.Domain.Interfaces.ISettingsRepository
    {
        public Task<POS.Domain.Entities.BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<POS.Domain.Entities.BusinessSettings?>(null);

        public Task SaveAsync(POS.Domain.Entities.BusinessSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubBackupService : IManualBackupService
    {
        public Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(destinationDirectory + "\\backup.sqlite");
    }

    private sealed class StubWhatsAppService : IWhatsAppService
    {
        public Task<bool> SendAsync(string phoneNumber, string message, string? attachmentPath, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
