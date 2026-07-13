using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.SmokeTests;

public sealed class AppBootstrapperSmokeTests
{
    [Fact]
    public async Task StartupBootstrapper_BuildsServices_SeedsAdmin_AndComposesShell()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "pos-startup-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        string dbPath = Path.Combine(workingDirectory, "pos-smoke.db");
        ServiceProvider? services = null;

        try
        {
            services = AppBootstrapper.BuildServiceProvider($"Data Source={dbPath}");
            await AppBootstrapper.EnsureLatestSchemaAsync(services, TestContext.Current.CancellationToken);

            using (IServiceScope scope = services.CreateScope())
            {
                var printerProvider = scope.ServiceProvider.GetRequiredService<IInstalledPrinterProvider>();
                var writableSession = scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                IReadOnlyList<string> installedPrinters = printerProvider.GetInstalledPrinters();
                User admin = await dbContext.Users.AsNoTracking()
                    .SingleAsync(user => user.Username == "admin", TestContext.Current.CancellationToken);

                writableSession.SetUser(
                    admin.Id,
                    admin.Username,
                    admin.RoleId,
                    POS.Domain.Authorization.RoleConstants.AdministratorRoleName,
                    new HashSet<string>(POS.Domain.Authorization.PermissionKeys.All));

                MainShellViewModel shell = AppBootstrapper.BuildMainShellViewModel(scope.ServiceProvider);

                Assert.NotNull(installedPrinters);
                Assert.Equal("cashier", shell.SelectedModule.Key);
                Assert.Contains(shell.FuncbarModules, module => module.Key == "admin");
                Assert.NotNull(shell.Settings);
                Assert.NotNull(shell.UserManagement);
            }
        }
        finally
        {
            services?.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }
}
