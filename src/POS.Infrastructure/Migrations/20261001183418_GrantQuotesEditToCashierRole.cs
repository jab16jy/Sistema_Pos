using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <summary>
    /// Delivers <c>PermissionKeys.Quotes.Edit</c> to installs whose Cashier role already exists.
    /// </summary>
    /// <remarks>
    /// The permission is a data change, not a schema change, so it belongs in a migration rather than
    /// in <c>DatabaseSchemaRollout</c>. A migration runs exactly once, inside the
    /// <c>SnapshotBeforePendingMigrationsAsync</c> envelope; the runtime seed it replaces ran on every
    /// launch with no snapshot, which is how a transient "database is locked" could escalate into a
    /// boot loop against a shop's live data. It also cannot restore a key an administrator
    /// deliberately removed: <c>RolePermission</c> carries no tombstone distinguishing "never granted"
    /// from "revoked", so a converging insert is indistinguishable from a privilege-escalation loop.
    ///
    /// The insert is conditional on the Cashier role already holding a grant set, which is what
    /// separates an install this migration has to repair from a fresh one. On a brand-new database the
    /// role row already exists by now — <c>ReplaceUserRoleEnumWithRbac</c> seeded it — but it holds
    /// zero permissions, and <c>SeedDefaultRolesAndAdminAsync</c> runs after migrations and grants the
    /// whole <c>CashierDefaults</c> set to exactly that case. Inserting here on a fresh install would
    /// leave the role with one row and make the seed's "zero rows" condition false, so the new shop
    /// would end up with a single permission. <c>OR IGNORE</c> keeps the statement safe against a
    /// database where the key is somehow already present.
    /// </remarks>
    public partial class GrantQuotesEditToCashierRole : Migration
    {
        private const string CashierRoleId = "00000000-0000-0000-0000-0000000000c2";
        private const string QuotesEditKey = "Quotes.Edit";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT OR IGNORE INTO "RolePermissions" ("RoleId", "PermissionKey")
                SELECT '{CashierRoleId}', '{QuotesEditKey}'
                WHERE EXISTS (
                    SELECT 1 FROM "RolePermissions" WHERE "RoleId" = '{CashierRoleId}'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes only the key this migration added. On a fresh install the same row came from
            // SeedDefaultRolesAndAdminAsync instead, and rolling back should still leave the Cashier
            // role without an edit permission it predates — which is the pre-migration state either way.
            migrationBuilder.Sql($"""
                DELETE FROM "RolePermissions"
                WHERE "RoleId" = '{CashierRoleId}' AND "PermissionKey" = '{QuotesEditKey}';
                """);
        }
    }
}
