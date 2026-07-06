using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Settings;

public sealed class SettingsUseCaseTests
{
    [Fact]
    public async Task LoadSettingsAsync_WhenSettingsExist_ReturnsSettings()
    {
        var settings = new BusinessSettings("Store", "Address", "Tax", "smtp.host", 587, "mailer", "secret", true, "ventas@store.com", "print", "C:/POS", "C:/Backups", "COP");
        var useCase = new LoadSettingsUseCase(new InMemorySettingsRepository(settings));

        SettingsResult? result = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(settings.Id, result.Id);
        Assert.Equal("smtp.host", result.SmtpHost);
        Assert.Equal(587, result.SmtpPort);
        Assert.Equal("ventas@store.com", result.SmtpSenderAddress);
        Assert.Equal("COP", result.CurrencyCode);
        Assert.Empty(result.Warnings ?? []);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSettingsDoNotExist_CreatesSettingsAndSaves()
    {
        var repository = new InMemorySettingsRepository(null);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new SaveSettingsUseCase(repository, unitOfWork);

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "Esthetic Medical Store",
            "Main Street 1",
            "900123",
            "smtp.office365.com",
            587,
            "ventas@esthetic.com",
            "app-password",
            true,
            "ventas@esthetic.com",
            "print-receipt",
            "C:/POS",
            "C:/Backups",
            "cop"), TestContext.Current.CancellationToken);

        Assert.Equal("Esthetic Medical Store", result.BusinessName);
        Assert.Equal("smtp.office365.com", result.SmtpHost);
        Assert.Equal("app-password", result.SmtpPassword);
        Assert.Equal("COP", result.CurrencyCode);
        Assert.NotNull(repository.Settings);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Empty(result.Warnings ?? []);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSettingsExist_UpdatesSameSettings()
    {
        var existing = new BusinessSettings("Old", null, null, null, null, null, null, false, null, null, null, null, "COP");
        var repository = new InMemorySettingsRepository(existing);
        var useCase = new SaveSettingsUseCase(repository, new CountingUnitOfWork());

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "New",
            null,
            null,
            "smtp.gmail.com",
            465,
            "new-user",
            "new-secret",
            true,
            "new@store.com",
            null,
            null,
            null,
            "usd"), TestContext.Current.CancellationToken);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("New", existing.BusinessName);
        Assert.Equal("smtp.gmail.com", existing.SmtpHost);
        Assert.Equal(465, existing.SmtpPort);
        Assert.Equal("new@store.com", existing.SmtpSenderAddress);
        Assert.Equal("USD", existing.CurrencyCode);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenPrinterCommandIsInvalid_ReturnsWarningAndStillSaves()
    {
        var repository = new InMemorySettingsRepository(null);
        var unitOfWork = new CountingUnitOfWork();
        var validator = new StubPrinterCommandValidator([
            new SettingsWarning("PrinterCommandMissing", "Printer command was not found.")
        ]);
        var useCase = new SaveSettingsUseCase(repository, unitOfWork, validator);

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "Store",
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "/missing/print-receipt",
            null,
            null,
            "COP"), TestContext.Current.CancellationToken);

        SettingsWarning warning = Assert.Single(result.Warnings ?? []);
        Assert.Equal("PrinterCommandMissing", warning.Code);
        Assert.NotNull(repository.Settings);
        Assert.Equal("/missing/print-receipt", repository.Settings.PrinterCommand);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TriggerManualBackupAsync_WithConfiguredDirectory_CreatesBackup()
    {
        var settings = new BusinessSettings("Store", null, null, null, null, null, null, false, null, null, null, "C:/Backups", "COP");
        var backupService = new StubManualBackupService("C:/Backups/backup-20260627.sqlite");
        var useCase = new TriggerManualBackupUseCase(new InMemorySettingsRepository(settings), backupService);

        ManualBackupResult result = await useCase.ExecuteAsync(
            new ManualBackupRequest(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Backup created successfully", result.Message);
        Assert.Equal("C:/Backups", backupService.LastDestinationDirectory);
        Assert.Equal("C:/Backups/backup-20260627.sqlite", result.BackupFilePath);
    }

    [Fact]
    public async Task TriggerManualBackupAsync_WhenNoDirectoryConfigured_ReturnsError()
    {
        var settings = new BusinessSettings("Store", null, null, null, null, null, null, false, null, null, null, null, "COP");
        var backupService = new StubManualBackupService("unused");
        var useCase = new TriggerManualBackupUseCase(new InMemorySettingsRepository(settings), backupService);

        ManualBackupResult result = await useCase.ExecuteAsync(
            new ManualBackupRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Backup directory is not configured.", result.Message);
        Assert.Null(backupService.LastDestinationDirectory);
    }

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        public InMemorySettingsRepository(BusinessSettings? settings)
        {
            Settings = settings;
        }

        public BusinessSettings? Settings { get; private set; }
        public int SaveCount { get; private set; }

        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Settings);

        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubPrinterCommandValidator : IPrinterCommandValidator
    {
        private readonly IReadOnlyList<SettingsWarning> _warnings;

        public StubPrinterCommandValidator(IReadOnlyList<SettingsWarning> warnings)
        {
            _warnings = warnings;
        }

        public Task<IReadOnlyList<SettingsWarning>> ValidateAsync(
            string? printerCommand,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_warnings);
    }

    private sealed class StubManualBackupService : IManualBackupService
    {
        private readonly string _backupFilePath;

        public StubManualBackupService(string backupFilePath)
        {
            _backupFilePath = backupFilePath;
        }

        public string? LastDestinationDirectory { get; private set; }

        public Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
        {
            LastDestinationDirectory = destinationDirectory;
            return Task.FromResult(_backupFilePath);
        }
    }
}
