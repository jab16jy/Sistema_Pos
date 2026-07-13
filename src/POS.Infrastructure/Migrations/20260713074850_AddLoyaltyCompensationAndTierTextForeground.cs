using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyCompensationAndTierTextForeground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TextForeground",
                table: "LOYALTY_TIERS",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS",
                column: "CompensatesTransactionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LOYALTY_TRANSACTIONS_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS",
                column: "CompensatesTransactionId",
                principalTable: "LOYALTY_TRANSACTIONS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LOYALTY_TRANSACTIONS_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.DropIndex(
                name: "IX_LOYALTY_TRANSACTIONS_CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.DropColumn(
                name: "CompensatesTransactionId",
                table: "LOYALTY_TRANSACTIONS");

            migrationBuilder.DropColumn(
                name: "TextForeground",
                table: "LOYALTY_TIERS");
        }
    }
}
