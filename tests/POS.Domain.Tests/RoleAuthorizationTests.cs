using POS.Domain.Authorization;
using POS.Domain.Entities;
using Xunit;

namespace POS.Domain.Tests;

public sealed class RoleAuthorizationTests
{
    [Fact]
    public void PermissionCatalog_ContainsGatedAndViewKeys_AndCashierDefaultsMatchOperationalSurface()
    {
        Assert.Contains(PermissionKeys.Users.Create, PermissionKeys.All);
        Assert.Contains(PermissionKeys.Roles.AssignPermissions, PermissionKeys.All);
        Assert.Contains(PermissionKeys.Admin.Backup.Restore, PermissionKeys.All);
        Assert.Contains(PermissionKeys.Products.View, PermissionKeys.All);
        Assert.Contains(PermissionKeys.Invoices.Cancel, PermissionKeys.All);

        var expected = new HashSet<string>
        {
            PermissionKeys.Cashier.Sell,
            PermissionKeys.Customers.View, PermissionKeys.Customers.Create, PermissionKeys.Customers.Edit,
            PermissionKeys.Quotes.View, PermissionKeys.Quotes.Create, PermissionKeys.Quotes.Convert,
            PermissionKeys.Invoices.View, PermissionKeys.Invoices.Reprint, PermissionKeys.Invoices.Return,
            PermissionKeys.Shifts.View, PermissionKeys.Shifts.Open, PermissionKeys.Shifts.Close
        };
        Assert.True(expected.SetEquals(PermissionKeys.CashierDefaults));
        Assert.DoesNotContain(PermissionKeys.Users.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Roles.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Audit.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Settings.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Admin.Backup.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Products.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Inventory.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Suppliers.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Reports.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Loyalty.View, PermissionKeys.CashierDefaults);
        Assert.DoesNotContain(PermissionKeys.Invoices.Cancel, PermissionKeys.CashierDefaults);
    }

    [Fact]
    public void RoleConstants_AreDistinctNonDefault_AndOnlyAdministratorBypasses()
    {
        Assert.NotEqual(Guid.Empty, RoleConstants.AdministratorRoleId);
        Assert.NotEqual(Guid.Empty, RoleConstants.CashierRoleId);
        Assert.NotEqual(RoleConstants.AdministratorRoleId, RoleConstants.CashierRoleId);
        Assert.True(RoleConstants.IsSystemAdministrator(RoleConstants.AdministratorRoleId));
        Assert.False(RoleConstants.IsSystemAdministrator(RoleConstants.CashierRoleId));
    }

    [Fact]
    public void Role_EnforcesNameAndSystemState()
    {
        Assert.Throws<ArgumentException>(() => new Role(" "));
        var role = Role.CreateSystemRole(RoleConstants.CashierRoleId, " Cashier ");
        Assert.True(role.IsSystem);
        Assert.Equal(RoleConstants.CashierRoleId, role.Id);
        role.Rename(" Sales ");
        Assert.Equal("Sales", role.Name);
        role.Deactivate();
        Assert.False(role.IsActive);
        Assert.Throws<ArgumentException>(() => role.Rename(" "));
    }

    [Fact]
    public void RolePermissionAndUser_RequireRoleIds()
    {
        Assert.Throws<ArgumentException>(() => new RolePermission(Guid.Empty, PermissionKeys.Users.View));
        Assert.Throws<ArgumentException>(() => new RolePermission(Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => new User("user", "hash", Guid.Empty));

        var user = new User("user", "hash", RoleConstants.CashierRoleId);
        user.ChangeRole(RoleConstants.AdministratorRoleId);
        Assert.Equal(RoleConstants.AdministratorRoleId, user.RoleId);
    }
}
