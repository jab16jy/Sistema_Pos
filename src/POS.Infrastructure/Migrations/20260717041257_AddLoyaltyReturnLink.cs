using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyReturnLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.AddColumn<string>(
                name: "SaleReturnId",
                table: "LOYALTY_TRANSACTIONS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId_SaleReturnId",
                table: "LOYALTY_TRANSACTIONS",
                columns: new[] { "CompensatesTransactionId", "SaleReturnId" },
                unique: true,
                filter: "\"CompensatesTransactionId\" IS NOT NULL AND \"SaleReturnId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId_SaleReturnId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.DropColumn(
                name: "SaleReturnId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS",
                column: "CompensatesTransactionId",
                unique: true);
        }
    }
}
