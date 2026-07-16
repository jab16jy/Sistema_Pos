using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivableSettlementCashierTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cashier_user_id",
                table: "RECEIVABLE_SETTLEMENTS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shift_id",
                table: "RECEIVABLE_SETTLEMENTS",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cashier_user_id",
                table: "RECEIVABLE_SETTLEMENTS");

            migrationBuilder.DropColumn(
                name: "shift_id",
                table: "RECEIVABLE_SETTLEMENTS");
        }
    }
}
