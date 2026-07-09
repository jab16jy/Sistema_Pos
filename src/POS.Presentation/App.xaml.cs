using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.BarcodeLabels;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Purchases;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Reporting;
using POS.Application.UseCases.Sales;
using POS.Application.UseCases.Settings;
using POS.Application.UseCases.Shifts;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Services;
using POS.Presentation.Shared.Shell;
using POS.Presentation.Shared.ViewModels;
using System.Text;

namespace POS.Presentation;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private IServiceScope? _scope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error de inicio");
            ex.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
            {
                System.Windows.MessageBox.Show(exception.ToString(), "Error no controlado");
            }

            Shutdown(1);
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error en tarea no observada");
            ex.SetObserved();
        };

        try
        {
            _services = AppBootstrapper.BuildServiceProvider();
            await AppBootstrapper.EnsureLatestSchemaAsync(_services).ConfigureAwait(true);
            _scope = _services.CreateScope();

            if (!ShowLoginAndLaunchShell())
                Shutdown(0);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Error de inicio");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scope?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    // Returns true if user logged in and shell was shown; false if user closed login
    private bool ShowLoginAndLaunchShell()
    {
        // Prevent app shutdown when login window closes (no windows open momentarily)
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var loginUseCase = _scope!.ServiceProvider.GetRequiredService<LoginUseCase>();
        var writableSession = _scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>();
        var loginVm = new ViewModels.LoginViewModel(loginUseCase, writableSession);
        var loginWindow = new Views.LoginWindow(loginVm);

        if (loginWindow.ShowDialog() != true)
            return false;

        MainShellViewModel shellVm;
        try
        {
            shellVm = AppBootstrapper.BuildMainShellViewModel(_scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error al construir el shell principal:\n\n{ex}",
                "Error de inicio");
            return false;
        }

        var shell = new Views.NavigationShellView
        {
            DataContext = shellVm
        };

        shellVm.LogoutRequested += () =>
        {
            ShellLogoutCoordinator.Execute(
                () => ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
                () =>
                {
                    var session = _scope.ServiceProvider.GetRequiredService<IWritableOperatorSession>();
                    Guid operatorId = session.OperatorId;
                    string username = session.Username;
                    session.ClearSession();

                    // Audit before the scope (and its AppDbContext) can be disposed by shutdown.
                    // Local SQLite write, not network I/O — blocking the UI thread briefly here is
                    // simpler and safer than a fire-and-forget task racing scope disposal.
                    try
                    {
                        _scope.ServiceProvider.GetRequiredService<LogoutUseCase>()
                            .ExecuteAsync(operatorId, username).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        // A failed audit write must not block the user from logging out, but must
                        // not vanish silently either — this app has no logging framework/dependency,
                        // so Trace is the stdlib option (rung 3) instead of adding one just for this.
                        System.Diagnostics.Trace.TraceError($"Logout audit write failed for user '{username}': {ex}");
                    }
                },
                shell.Close,
                ShowLoginAndLaunchShell,
                () => Shutdown(0));
        };

        MainWindow = shell;
        shell.Show();

        // Now that shell is open, restore normal shutdown behavior
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        return true;
    }

}
