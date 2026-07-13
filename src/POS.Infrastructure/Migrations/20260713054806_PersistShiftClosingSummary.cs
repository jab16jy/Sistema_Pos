using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistShiftClosingSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_payments",
                table: "CASHIER_SHIFTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "digital_payments",
                table: "CASHIER_SHIFTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "has_closing_summary",
                table: "CASHIER_SHIFTS",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "sales_total",
                table: "CASHIER_SHIFTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cash_payments",
                table: "CASHIER_SHIFTS");

            migrationBuilder.DropColumn(
                name: "digital_payments",
                table: "CASHIER_SHIFTS");

            migrationBuilder.DropColumn(
                name: "has_closing_summary",
                table: "CASHIER_SHIFTS");

            migrationBuilder.DropColumn(
                name: "sales_total",
                table: "CASHIER_SHIFTS");
        }
    }
}
