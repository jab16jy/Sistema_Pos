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
            new TriggerManualBackupUseCase(new StubMutableSettingsRepository(), new StubBackupService(), new StubUnitOfWork(), new StubOperatorSession()),
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

    [Fact]
    public async Task BackupAsync_WhenBackupSucceeds_UpdatesLastSuccessfulBackupMetadata()
    {
        var settings = new POS.Domain.Entities.BusinessSettings(
            "Store", null, null, null, null, null, null, null, false, null, null, null, "C:/Backups", "COP");
        var repository = new StubMutableSettingsRepository(settings);
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(repository),
            new SaveSettingsUseCase(repository, new StubUnitOfWork()),
            new TriggerManualBackupUseCase(repository, new StubBackupService(), new StubUnitOfWork(), new StubOperatorSession()));

        await viewModel.BackupAsync(TestContext.Current.CancellationToken);

        Assert.Equal("2026-06-27 10:30:00 UTC", viewModel.LastBackupTimestampUtc);
        Assert.Equal("1024 bytes", viewModel.LastBackupSize);
        Assert.Equal("C:/Backups/backup.sqlite", viewModel.LastBackupLocation);
        Assert.Equal("maria", viewModel.LastBackupOperator);
        Assert.Contains("validado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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
                _settings.BusinessPhone,
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
        private POS.Domain.Entities.BusinessSettings? _settings;

        public StubMutableSettingsRepository(POS.Domain.Entities.BusinessSettings? settings = null)
        {
            _settings = settings;
        }

        public Task<POS.Domain.Entities.BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);

        public Task SaveAsync(POS.Domain.Entities.BusinessSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubBackupService : IManualBackupService
    {
        public Task<BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupFile("C:/Backups/backup.sqlite", new DateTimeOffset(2026, 6, 27, 10, 30, 0, TimeSpan.Zero), 1024));
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.NewGuid();
        public string Username => "maria";
        public POS.Domain.Enums.UserRole Role => POS.Domain.Enums.UserRole.Administrator;
        public bool IsAuthenticated => true;
    }

    private sealed class StubWhatsAppService : IWhatsAppService
    {
        public Task<bool> SendAsync(string phoneNumber, string message, string? attachmentPath, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
