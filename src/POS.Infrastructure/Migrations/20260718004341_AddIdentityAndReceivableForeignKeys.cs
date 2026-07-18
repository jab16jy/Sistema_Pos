using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndReceivableForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding foreign keys forces SQLite to rebuild each table, which fails outright
            // if any row already points at a deleted principal. Clean up dangling references first
            // so the rebuild succeeds regardless of how the orphan got there (manual edits, old bugs, etc).
            migrationBuilder.Sql(
                """
                DELETE FROM "CASHIER_SHIFTS"
                WHERE "CashierUserId" NOT IN (SELECT "Id" FROM "Users");
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "RECEIVABLES"
                WHERE "CustomerId" NOT IN (SELECT "Id" FROM "Customers")
                   OR "SaleId" NOT IN (SELECT "Id" FROM "SALES");
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "SALE_RETURNS"
                WHERE "ProcessedByUserId" NOT IN (SELECT "Id" FROM "Users");
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "SALES"
                WHERE "CashierUserId" NOT IN (SELECT "Id" FROM "Users");
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
