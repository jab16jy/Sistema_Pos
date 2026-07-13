using POS.Domain.Authorization;
using POS.Presentation.Converters;
using Xunit;

namespace POS.Presentation.SmokeTests;

/// <summary>
/// RoleConstants must stay in English (it seeds/matches the persisted Role.Name in the database) —
/// only the UI label is translated. See RoleDisplayNames.
/// </summary>
public sealed class RoleDisplayNamesTests
{
    [Theory]
    [InlineData(RoleConstants.AdministratorRoleName, "Administrador")]
    [InlineData(RoleConstants.CashierRoleName, "Cajero")]
    [InlineData("Supervisor", "Supervisor")]
    [InlineData(null, "")]
    public void ToDisplayName_TranslatesOnlySystemRoles(string? name, string expected)
    {
        Assert.Equal(expected, RoleDisplayNames.ToDisplayName(name));
    }

    [Fact]
    public void SystemRoleConstants_StayInEnglish()
    {
        // Guards against re-introducing the backend-corrupting fix: RoleConstants feeds seed data
        // and DatabaseSchemaRollout's rename-on-mismatch logic, so it must never hold Spanish text.
        Assert.Equal("Administrator", RoleConstants.AdministratorRoleName);
        Assert.Equal("Cashier", RoleConstants.CashierRoleName);
    }
}
