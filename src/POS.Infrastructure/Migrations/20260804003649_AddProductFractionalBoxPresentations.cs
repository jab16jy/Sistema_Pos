using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFractionalBoxPresentations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "half_box_price",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "half_box_units",
                table: "PRODUCTS",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quarter_box_price",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quarter_box_units",
                table: "PRODUCTS",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "half_box_price",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "half_box_units",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "quarter_box_price",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "quarter_box_units",
                table: "PRODUCTS");
        }
    }
}
