using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropSaleItemTaxRateDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "tax_rate",
                table: "SALE_ITEMS",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldDefaultValue: 19m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "tax_rate",
                table: "SALE_ITEMS",
                type: "TEXT",
                nullable: false,
                defaultValue: 19m,
                oldClrType: typeof(decimal),
                oldType: "TEXT");
        }
    }
}
