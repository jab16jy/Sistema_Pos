using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLinePresentationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "presentation",
                table: "SALE_ITEMS",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "units_per_presentation",
                table: "SALE_ITEMS",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "presentation",
                table: "QUOTE_ITEMS",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "units_per_presentation",
                table: "QUOTE_ITEMS",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "presentation",
                table: "SALE_ITEMS");

            migrationBuilder.DropColumn(
                name: "units_per_presentation",
                table: "SALE_ITEMS");

            migrationBuilder.DropColumn(
                name: "presentation",
                table: "QUOTE_ITEMS");

            migrationBuilder.DropColumn(
                name: "units_per_presentation",
                table: "QUOTE_ITEMS");
        }
    }
}
