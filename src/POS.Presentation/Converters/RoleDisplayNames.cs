using POS.Domain.Authorization;

namespace POS.Presentation.Converters;

/// <summary>
/// Translates the two built-in system role names for display only. The persisted
/// <c>Role.Name</c> (and <see cref="RoleConstants"/>) must stay in English — this never touches
/// the database, it only decides what the UI shows. Custom roles pass through unchanged.
/// </summary>
public static class RoleDisplayNames
{
    public static string ToDisplayName(string? name) => name switch
    {
        RoleConstants.AdministratorRoleName => "Administrador",
        RoleConstants.CashierRoleName => "Cajero",
        _ => name ?? string.Empty
    };
}
