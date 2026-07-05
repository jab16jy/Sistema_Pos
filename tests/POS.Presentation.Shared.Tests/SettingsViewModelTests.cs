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
}
