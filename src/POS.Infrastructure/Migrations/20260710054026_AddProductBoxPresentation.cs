using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBoxPresentation : Migration
    {
        // Scoped to the box-presentation feature: adds the two optional PRODUCTS columns only.
        // The scaffolder also surfaced pre-existing model/snapshot drift unrelated to this change
        // (audit-log indexes, legacy TempId shadow keys, DateTimeOffset column types); that drift
        // was intentionally left out so it stays owned by whoever introduced it, not this migration.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "box_price",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "units_per_box",
                table: "PRODUCTS",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "box_price",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "units_per_box",
                table: "PRODUCTS");
        }
    }
}
