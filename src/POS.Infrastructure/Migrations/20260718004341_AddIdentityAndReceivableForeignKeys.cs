using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndReceivableForeignKeys : Migration
    {
        private const string CashierRoleId = "00000000-0000-0000-0000-0000000000c2";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding foreign keys forces SQLite to rebuild each table, which fails outright
            // if any row already points at a deleted principal. Dangling references must go before
            // the rebuild, regardless of how the orphan got there (manual edits, old bugs, etc).
            //
            // ponytail: recreate the missing user instead of deleting the rows that point at it.
            // Sales, returns and shifts are financial history — deleting them to satisfy a constraint
            // destroys exactly the data the FK exists to protect. The placeholder is inactive and its
            // hash matches nothing, so LoginUseCase rejects it; the Guid in the username keeps it
            // unique under IX_Users_Username. Restoring the real user later is a rename away.
            migrationBuilder.Sql(
                $"""
                INSERT INTO "Users" ("Id", "Username", "PasswordHash", "RoleId", "IsActive", "CreatedAt")
                SELECT "Id",
                       'usuario_eliminado_' || "Id",
                       '!',
                       '{CashierRoleId}',
                       0,
                       '2000-01-01 00:00:00.0000000+00:00'
                FROM (
                    SELECT "CashierUserId" AS "Id" FROM "SALES"
                    UNION
                    SELECT "CashierUserId" FROM "CASHIER_SHIFTS"
                    UNION
                    SELECT "ProcessedByUserId" FROM "SALE_RETURNS"
                )
                WHERE "Id" IS NOT NULL
                  AND "Id" NOT IN (SELECT "Id" FROM "Users");
                """);

            // Runs after the user backfill so receivables are only dropped for a genuinely missing
            // customer or sale. A receivable whose customer no longer exists has nobody left to
            // collect from, and unlike a user there is no safe identity to invent for it.
            migrationBuilder.Sql(
                """
                DELETE FROM "RECEIVABLES"
                WHERE "CustomerId" NOT IN (SELECT "Id" FROM "Customers")
                   OR "SaleId" NOT IN (SELECT "Id" FROM "SALES");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SALES_CashierUserId",
                table: "SALES",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_ProcessedByUserId",
                table: "SALE_RETURNS",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIVABLES_CustomerId",
                table: "RECEIVABLES",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIVABLES_SaleId",
                table: "RECEIVABLES",
                column: "SaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_CASHIER_SHIFTS_Users_CashierUserId",
                table: "CASHIER_SHIFTS",
                column: "CashierUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RECEIVABLES_Customers_CustomerId",
                table: "RECEIVABLES",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RECEIVABLES_SALES_SaleId",
                table: "RECEIVABLES",
                column: "SaleId",
                principalTable: "SALES",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SALE_RETURNS_Users_ProcessedByUserId",
                table: "SALE_RETURNS",
                column: "ProcessedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SALES_Users_CashierUserId",
                table: "SALES",
                column: "CashierUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CASHIER_SHIFTS_Users_CashierUserId",
                table: "CASHIER_SHIFTS");

            migrationBuilder.DropForeignKey(
                name: "FK_RECEIVABLES_Customers_CustomerId",
                table: "RECEIVABLES");

            migrationBuilder.DropForeignKey(
                name: "FK_RECEIVABLES_SALES_SaleId",
                table: "RECEIVABLES");

            migrationBuilder.DropForeignKey(
                name: "FK_SALE_RETURNS_Users_ProcessedByUserId",
                table: "SALE_RETURNS");

            migrationBuilder.DropForeignKey(
                name: "FK_SALES_Users_CashierUserId",
                table: "SALES");

            migrationBuilder.DropIndex(
                name: "IX_SALES_CashierUserId",
                table: "SALES");

            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURNS_ProcessedByUserId",
                table: "SALE_RETURNS");

            migrationBuilder.DropIndex(
                name: "IX_RECEIVABLES_CustomerId",
                table: "RECEIVABLES");

            migrationBuilder.DropIndex(
                name: "IX_RECEIVABLES_SaleId",
                table: "RECEIVABLES");
        }
    }
}
