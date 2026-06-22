using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashClosingSeparatedTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "billed_total",
                table: "CASH_CLOSINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cash_collected",
                table: "CASH_CLOSINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "digital_collected",
                table: "CASH_CLOSINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "receivable_settlement_total",
                table: "CASH_CLOSINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "billed_total",
                table: "CASH_CLOSINGS");

            migrationBuilder.DropColumn(
                name: "cash_collected",
                table: "CASH_CLOSINGS");

            migrationBuilder.DropColumn(
                name: "digital_collected",
                table: "CASH_CLOSINGS");

            migrationBuilder.DropColumn(
                name: "receivable_settlement_total",
                table: "CASH_CLOSINGS");
        }
    }
}
