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
            "COM4",
            null,
            "COP",
            []);
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(new StubSettingsRepository(settings), new StubOperatorSession()),
            new SaveSettingsUseCase(new StubMutableSettingsRepository(), new StubUnitOfWork(), new StubOperatorSession()),
            emailService: null,
            whatsAppService: new StubWhatsAppService());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("smtp.office365.com", viewModel.SmtpHost);
        Assert.Equal("587", viewModel.SmtpPort);
        Assert.Equal("ventas@store.com", viewModel.SmtpUser);
        Assert.Equal("secret", viewModel.SmtpPassword);
        Assert.True(viewModel.SmtpUseSsl);
        Assert.True(viewModel.IsWhatsAppReady);
        Assert.Equal("COM4", viewModel.PrinterPort);
        Assert.Null(viewModel.PrinterCommand);
        Assert.True(viewModel.IsPrinterConfigured);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsMaximumProductPresentations()
    {
        var repository = new StubMutableSettingsRepository();
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(repository, new StubOperatorSession()),
            new SaveSettingsUseCase(repository, new StubUnitOfWork(), new StubOperatorSession()))
        {
            BusinessName = "Store",
            CurrencyCode = "COP",
            MaximumProductPresentations = 3,
        };

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        var reloaded = new SettingsViewModel(
            new LoadSettingsUseCase(repository, new StubOperatorSession()),
            new SaveSettingsUseCase(repository, new StubUnitOfWork(), new StubOperatorSession()));

        await reloaded.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, reloaded.MaximumProductPresentations);
    }

    [Fact]
    public async Task LoadAsync_GeneratesDynamicSystemAlerts()
    {
        var settings = new SettingsResult(
            Guid.NewGuid(),
            "Store",
            null, null, null, null, null, null, null, false, null, null, null, null, null, "COP");
        var viewModel = new SettingsViewModel(
            new LoadSettingsUseCase(new StubSettingsRepository(settings), new StubOperatorSession()),
            new SaveSettingsUseCase(new StubMutableSettingsRepository(), new StubUnitOfWork(), new StubOperatorSession()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(viewModel.SystemAlerts);
        Assert.Contains(viewModel.SystemAlerts, a => a.IsWarning && a.Message.Contains("impresora"));
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
                _settings.PrinterPort,
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

        public Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

    private sealed class StubWhatsAppService : IWhatsAppService
    {
        public Task<bool> SendAsync(string phoneNumber, string message, string? attachmentPath, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
