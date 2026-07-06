using POS.Presentation.Shared.Shell;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class ShellLogoutCoordinatorTests
{
    [Fact]
    public void Execute_SetsExplicitShutdownBeforeClosingShell_AndDoesNotShutdownWhenLoginReturns()
    {
        var calls = new List<string>();

        ShellLogoutCoordinator.Execute(
            () => calls.Add("mode"),
            () => calls.Add("clear"),
            () => calls.Add("close"),
            () =>
            {
                calls.Add("restart");
                return true;
            },
            () => calls.Add("shutdown"));

        Assert.Equal(["mode", "clear", "close", "restart"], calls);
    }

    [Fact]
    public void Execute_ShutsDownWhenLoginFlowDoesNotRestart()
    {
        var calls = new List<string>();

        ShellLogoutCoordinator.Execute(
            () => calls.Add("mode"),
            () => calls.Add("clear"),
            () => calls.Add("close"),
            () => false,
            () => calls.Add("shutdown"));

        Assert.Contains("shutdown", calls);
    }
}
