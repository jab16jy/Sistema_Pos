using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleItemIdToSaleReturnItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SaleItemId",
                table: "SALE_RETURN_ITEMS",
                type: "TEXT",
                nullable: false,
                defaultValue: "00000000-0000-0000-0000-000000000000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaleItemId",
                table: "SALE_RETURN_ITEMS");
        }
    }
}
