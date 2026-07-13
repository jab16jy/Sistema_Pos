namespace POS.Domain.Authorization;

public static class RoleConstants
{
    public static readonly Guid AdministratorRoleId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid CashierRoleId = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
    public const string AdministratorRoleName = "Administrator";
    public const string CashierRoleName = "Cashier";

    public static bool IsSystemAdministrator(Guid roleId) => roleId == AdministratorRoleId;
}
