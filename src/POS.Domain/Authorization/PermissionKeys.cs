using System.Reflection;

namespace POS.Domain.Authorization;

public static class PermissionKeys
{
    public static class Users { public const string View = "Users.View"; public const string Create = "Users.Create"; public const string Edit = "Users.Edit"; public const string Deactivate = "Users.Deactivate"; public const string Activate = "Users.Activate"; public const string Delete = "Users.Delete"; }
    public static class Roles { public const string View = "Roles.View"; public const string Create = "Roles.Create"; public const string Edit = "Roles.Edit"; public const string Deactivate = "Roles.Deactivate"; public const string AssignPermissions = "Roles.AssignPermissions"; }
    public static class Audit { public const string View = "Audit.View"; }
    public static class Settings { public const string View = "Settings.View"; public const string Edit = "Settings.Edit"; }
    public static class Admin { public static class Backup { public const string View = "Admin.Backup.View"; public const string Create = "Admin.Backup.Create"; public const string Restore = "Admin.Backup.Restore"; } }
    public static class Invoices { public const string View = "Invoices.View"; public const string Reprint = "Invoices.Reprint"; public const string Return = "Invoices.Return"; public const string Cancel = "Invoices.Cancel"; }
    public static class Cashier { public const string Sell = "Cashier.Sell"; }
    public static class Products { public const string View = "Products.View"; public const string Create = "Products.Create"; public const string Edit = "Products.Edit"; public const string Delete = "Products.Delete"; public const string Import = "Products.Import"; }
    public static class Categories { public const string View = "Categories.View"; public const string Create = "Categories.Create"; public const string Edit = "Categories.Edit"; public const string Delete = "Categories.Delete"; }
    public static class Inventory { public const string View = "Inventory.View"; public const string Adjust = "Inventory.Adjust"; }
    public static class Quotes { public const string View = "Quotes.View"; public const string Create = "Quotes.Create"; public const string Convert = "Quotes.Convert"; }
    public static class Suppliers { public const string View = "Suppliers.View"; public const string Create = "Suppliers.Create"; public const string Edit = "Suppliers.Edit"; public const string Delete = "Suppliers.Delete"; }
    public static class Purchases { public const string View = "Purchases.View"; public const string Create = "Purchases.Create"; }
    public static class Customers { public const string View = "Customers.View"; public const string Create = "Customers.Create"; public const string Edit = "Customers.Edit"; }
    public static class Receivables { public const string View = "Receivables.View"; public const string ApplyPayment = "Receivables.ApplyPayment"; }
    public static class Reports { public const string View = "Reports.View"; }
    public static class CashRegister { public const string Close = "CashRegister.Close"; }
    public static class Shifts { public const string View = "Shifts.View"; public const string Open = "Shifts.Open"; public const string Close = "Shifts.Close"; }
    public static class Loyalty { public const string View = "Loyalty.View"; public const string Redeem = "Loyalty.Redeem"; public const string ConfigureRules = "Loyalty.ConfigureRules"; public const string ManageTiers = "Loyalty.ManageTiers"; }
    public static class Barcode { public const string View = "Barcode.View"; public const string Print = "Barcode.Print"; }
    public static class Documents { public const string Send = "Documents.Send"; }

    public static IReadOnlyList<string> All { get; } = BuildAll();

    public static IReadOnlySet<string> CashierDefaults { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Cashier.Sell,
        Customers.View, Customers.Create, Customers.Edit,
        Quotes.View, Quotes.Create, Quotes.Convert,
        Invoices.View, Invoices.Reprint, Invoices.Return,
        Shifts.View, Shifts.Open, Shifts.Close
    };

    private static IReadOnlyList<string> BuildAll()
    {
        var keys = new List<string>();
        void Collect(Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral && field.FieldType == typeof(string)))
                keys.Add((string)field.GetRawConstantValue()!);
            foreach (var nested in type.GetNestedTypes())
                Collect(nested);
        }

        Collect(typeof(PermissionKeys));
        return keys.Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
