using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Settings;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Settings;

public sealed class SettingsUseCaseTests
{
    [Fact]
    public async Task LoadSettingsAsync_WhenSettingsExist_ReturnsSettings()
    {
        var settings = new BusinessSettings("Store", "Address", "Tax", "3005550000", "smtp.host", 587, "mailer", "secret", true, "ventas@store.com", "print", "C:/POS", "COM3", "C:/Backups", "COP");
        var useCase = new LoadSettingsUseCase(new InMemorySettingsRepository(settings), new StubOperatorSession("settings"));

        SettingsResult? result = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(settings.Id, result.Id);
        Assert.Equal("smtp.host", result.SmtpHost);
        Assert.Equal(587, result.SmtpPort);
        Assert.Equal("ventas@store.com", result.SmtpSenderAddress);
        Assert.Equal("COM3", result.PrinterPort);
        Assert.Equal("COP", result.CurrencyCode);
        Assert.Empty(result.Warnings ?? []);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSettingsDoNotExist_CreatesSettingsAndSaves()
    {
        var repository = new InMemorySettingsRepository(null);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new SaveSettingsUseCase(repository, unitOfWork, new StubOperatorSession("settings"));

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "Esthetic Medical Store",
            "Main Street 1",
            "900123",
            "301 5225337",
            "smtp.office365.com",
            587,
            "ventas@esthetic.com",
            "app-password",
            true,
            "ventas@esthetic.com",
            "print-receipt",
            "C:/POS",
            "192.168.1.10:9100",
            "C:/Backups",
            "cop"), TestContext.Current.CancellationToken);

        Assert.Equal("Esthetic Medical Store", result.BusinessName);
        Assert.Equal("smtp.office365.com", result.SmtpHost);
        Assert.Equal("app-password", result.SmtpPassword);
        Assert.Equal("192.168.1.10:9100", result.PrinterPort);
        Assert.Equal("COP", result.CurrencyCode);
        Assert.NotNull(repository.Settings);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Empty(result.Warnings ?? []);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSettingsExist_UpdatesSameSettings()
    {
        var existing = new BusinessSettings("Old", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP");
        var repository = new InMemorySettingsRepository(existing);
        var useCase = new SaveSettingsUseCase(repository, new CountingUnitOfWork(), new StubOperatorSession("settings"));

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "New",
            null,
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
        var useCase = new SaveSettingsUseCase(repository, unitOfWork, new StubOperatorSession("settings"), validator);

        SettingsResult result = await useCase.ExecuteAsync(new SaveSettingsRequest(
            "Store",
            null,
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
        var settings = new BusinessSettings("Store", null, null, null, null, null, null, null, false, null, null, null, null, "C:/Backups", "COP");
        var backupService = new StubManualBackupService("C:/Backups/backup-20260627.sqlite");
        var repository = new InMemorySettingsRepository(settings);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new TriggerManualBackupUseCase(repository, backupService, unitOfWork, new StubOperatorSession("maria"));

        ManualBackupResult result = await useCase.ExecuteAsync(
            new ManualBackupRequest(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Backup created and validated successfully.", result.Message);
        Assert.Equal("C:/Backups", backupService.LastDestinationDirectory);
        Assert.Equal("C:/Backups/backup-20260627.sqlite", result.BackupFilePath);
        Assert.NotNull(result.LastSuccessfulBackup);
        Assert.Equal("maria", result.LastSuccessfulBackup.OperatorUsername);
        Assert.Equal(1024, result.LastSuccessfulBackup.SizeBytes);
        Assert.Equal(result.LastSuccessfulBackup.TimestampUtc, settings.LastBackupAtUtc);
        Assert.Equal(result.LastSuccessfulBackup.Location, settings.LastBackupLocation);
        Assert.Equal("maria", settings.LastBackupUsername);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TriggerManualBackupAsync_WhenNoDirectoryConfigured_ReturnsError()
    {
        var settings = new BusinessSettings("Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP");
        var backupService = new StubManualBackupService("unused");
        var useCase = new TriggerManualBackupUseCase(
            new InMemorySettingsRepository(settings),
            backupService,
            new CountingUnitOfWork(),
            new StubOperatorSession("maria"));

        ManualBackupResult result = await useCase.ExecuteAsync(
            new ManualBackupRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Backup directory is not configured.", result.Message);
        Assert.Null(backupService.LastDestinationDirectory);
    }

    [Fact]
    public async Task BackupCreateAndRestore_RequireDistinctPermissions()
    {
        var settings = new BusinessSettings("Store", null, null, null, null, null, null, null, false, null, null, null, null, "C:/Backups", "COP");
        var backupService = new StubManualBackupService("C:/Backups/backup.sqlite");
        var createOnly = new StubOperatorSession("maria", new HashSet<string> { PermissionKeys.Admin.Backup.Create });

        ManualBackupResult created = await new TriggerManualBackupUseCase(new InMemorySettingsRepository(settings), backupService, new CountingUnitOfWork(), createOnly)
            .ExecuteAsync(new ManualBackupRequest(), TestContext.Current.CancellationToken);
        RestoreBackupResult deniedRestore = await new RestoreBackupUseCase(backupService, createOnly)
            .ExecuteAsync(new RestoreBackupRequest("C:/Backups/backup.sqlite"), TestContext.Current.CancellationToken);
        RestoreBackupResult restored = await new RestoreBackupUseCase(backupService, new StubOperatorSession("maria", new HashSet<string> { PermissionKeys.Admin.Backup.Restore }))
            .ExecuteAsync(new RestoreBackupRequest("C:/Backups/backup.sqlite"), TestContext.Current.CancellationToken);
        var noPermissions = new StubOperatorSession("maria", new HashSet<string>());
        ManualBackupResult deniedCreate = await new TriggerManualBackupUseCase(new InMemorySettingsRepository(settings), backupService, new CountingUnitOfWork(), noPermissions)
            .ExecuteAsync(new ManualBackupRequest(), TestContext.Current.CancellationToken);
        RestoreBackupResult deniedBoth = await new RestoreBackupUseCase(backupService, noPermissions)
            .ExecuteAsync(new RestoreBackupRequest("C:/Backups/backup.sqlite"), TestContext.Current.CancellationToken);

        Assert.True(created.Succeeded);
        Assert.False(deniedRestore.Succeeded);
        Assert.True(restored.Succeeded);
        Assert.False(deniedCreate.Succeeded);
        Assert.False(deniedBoth.Succeeded);
    }

    [Fact]
    public async Task SettingsGates_RequireViewAndEditWithoutMutatingOnDenial()
    {
        var repository = new InMemorySettingsRepository(null);
        var unitOfWork = new CountingUnitOfWork();
        var noPermissions = new StubOperatorSession("maria", new HashSet<string>());

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new LoadSettingsUseCase(repository, noPermissions).ExecuteAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new SaveSettingsUseCase(repository, unitOfWork, noPermissions).ExecuteAsync(new SaveSettingsRequest("Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP"), TestContext.Current.CancellationToken));
        Assert.Null(repository.Settings);

        var editSession = new StubOperatorSession("maria", new HashSet<string> { PermissionKeys.Settings.Edit });
        SettingsResult saved = await new SaveSettingsUseCase(repository, unitOfWork, editSession).ExecuteAsync(new SaveSettingsRequest("Store", null, null, null, null, null, null, null, false, null, null, null, null, "C:/Initial", "COP"), TestContext.Current.CancellationToken);
        bool updated = await new UpdateBackupDirectoryUseCase(repository, unitOfWork, editSession).ExecuteAsync("C:/Updated", TestContext.Current.CancellationToken);
        Assert.Equal("C:/Updated", repository.Settings!.BackupDirectory);
        Assert.True(updated);
        Assert.NotEqual(Guid.Empty, saved.Id);
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
        private readonly BackupFile _backupFile;

        public StubManualBackupService(string backupFilePath)
        {
            _backupFile = new BackupFile(backupFilePath, new DateTimeOffset(2026, 6, 27, 10, 30, 0, TimeSpan.Zero), 1024);
        }

        public string? LastDestinationDirectory { get; private set; }

        public Task<BackupFile> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default)
        {
            LastDestinationDirectory = destinationDirectory;
            return Task.FromResult(_backupFile);
        }

        public Task RestoreBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public StubOperatorSession(string username, IReadOnlySet<string>? permissions = null)
        {
            Username = username;
            Permissions = permissions ?? new HashSet<string>(PermissionKeys.All);
        }

        public Guid OperatorId => Guid.NewGuid();
        public string Username { get; }
        public Guid RoleId => POS.Domain.Authorization.RoleConstants.AdministratorRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.AdministratorRoleName;
        public IReadOnlySet<string> Permissions { get; }
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }
}
