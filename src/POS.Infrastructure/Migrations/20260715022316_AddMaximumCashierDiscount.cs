using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaximumCashierDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumCashierDiscountPercent",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 20m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumCashierDiscountPercent",
                table: "BUSINESS_SETTINGS");
        }
    }
}
