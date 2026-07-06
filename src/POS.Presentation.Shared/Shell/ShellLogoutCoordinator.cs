namespace POS.Presentation.Shared.Shell;

public static class ShellLogoutCoordinator
{
    public static void Execute(
        Action setExplicitShutdownMode,
        Action clearSession,
        Action closeShell,
        Func<bool> restartLoginFlow,
        Action shutdownApplication)
    {
        ArgumentNullException.ThrowIfNull(setExplicitShutdownMode);
        ArgumentNullException.ThrowIfNull(clearSession);
        ArgumentNullException.ThrowIfNull(closeShell);
        ArgumentNullException.ThrowIfNull(restartLoginFlow);
        ArgumentNullException.ThrowIfNull(shutdownApplication);

        setExplicitShutdownMode();
        clearSession();
        closeShell();

        if (!restartLoginFlow())
        {
            shutdownApplication();
        }
    }
}
