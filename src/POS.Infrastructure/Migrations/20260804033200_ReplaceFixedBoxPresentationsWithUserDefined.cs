using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceFixedBoxPresentationsWithUserDefined : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PRODUCT_PRESENTATIONS",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    product_id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    units = table.Column<int>(type: "INTEGER", nullable: false),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_PRESENTATIONS", x => x.id);
                    table.ForeignKey(
                        name: "FK_PRODUCT_PRESENTATIONS_PRODUCTS_product_id",
                        column: x => x.product_id,
                        principalTable: "PRODUCTS",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_PRESENTATIONS_product_id_name",
                table: "PRODUCT_PRESENTATIONS",
                columns: new[] { "product_id", "name" },
                unique: true);

            // Backfill box presentations into PRODUCT_PRESENTATIONS table
            migrationBuilder.Sql(@"
                INSERT INTO PRODUCT_PRESENTATIONS (id, product_id, name, units, price, display_order)
                SELECT 
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89ab', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                    id, 'Caja', units_per_box, box_price, 0
                FROM PRODUCTS WHERE units_per_box IS NOT NULL AND box_price IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO PRODUCT_PRESENTATIONS (id, product_id, name, units, price, display_order)
                SELECT 
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89ab', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                    id, 'Media caja', half_box_units, half_box_price, 1
                FROM PRODUCTS WHERE half_box_units IS NOT NULL AND half_box_price IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO PRODUCT_PRESENTATIONS (id, product_id, name, units, price, display_order)
                SELECT 
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89ab', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                    id, 'Cuarto de caja', quarter_box_units, quarter_box_price, 2
                FROM PRODUCTS WHERE quarter_box_units IS NOT NULL AND quarter_box_price IS NOT NULL;
            ");

            // Add presentation_name column to SALE_ITEMS and QUOTE_ITEMS
            migrationBuilder.AddColumn<string>(
                name: "presentation_name",
                table: "SALE_ITEMS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "presentation_name",
                table: "QUOTE_ITEMS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Backfill presentation_name from integer presentation enum
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation_name = 'Caja' WHERE presentation = 2;");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation_name = 'Media caja' WHERE presentation = 3;");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation_name = 'Cuarto de caja' WHERE presentation = 4;");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation_name = NULL WHERE presentation = 1;");

            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation_name = 'Caja' WHERE presentation = 2;");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation_name = 'Media caja' WHERE presentation = 3;");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation_name = 'Cuarto de caja' WHERE presentation = 4;");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation_name = NULL WHERE presentation = 1;");

            // Drop old presentation columns
            migrationBuilder.DropColumn(name: "presentation", table: "SALE_ITEMS");
            migrationBuilder.DropColumn(name: "presentation", table: "QUOTE_ITEMS");

            migrationBuilder.DropColumn(name: "units_per_box", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "box_price", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "half_box_units", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "half_box_price", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "quarter_box_units", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "quarter_box_price", table: "PRODUCTS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "units_per_box", table: "PRODUCTS", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "box_price", table: "PRODUCTS", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "half_box_units", table: "PRODUCTS", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "half_box_price", table: "PRODUCTS", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "quarter_box_units", table: "PRODUCTS", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "quarter_box_price", table: "PRODUCTS", type: "TEXT", nullable: true);

            migrationBuilder.AddColumn<int>(name: "presentation", table: "SALE_ITEMS", type: "INTEGER", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(name: "presentation", table: "QUOTE_ITEMS", type: "INTEGER", nullable: false, defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE PRODUCTS SET 
                    units_per_box = (SELECT units FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Caja'),
                    box_price = (SELECT price FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Caja'),
                    half_box_units = (SELECT units FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Media caja'),
                    half_box_price = (SELECT price FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Media caja'),
                    quarter_box_units = (SELECT units FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Cuarto de caja'),
                    quarter_box_price = (SELECT price FROM PRODUCT_PRESENTATIONS WHERE product_id = PRODUCTS.id AND name = 'Cuarto de caja');
            ");

            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation = 2 WHERE presentation_name = 'Caja';");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation = 3 WHERE presentation_name = 'Media caja';");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation = 4 WHERE presentation_name = 'Cuarto de caja';");
            migrationBuilder.Sql("UPDATE SALE_ITEMS SET presentation = 1 WHERE presentation_name IS NULL;");

            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation = 2 WHERE presentation_name = 'Caja';");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation = 3 WHERE presentation_name = 'Media caja';");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation = 4 WHERE presentation_name = 'Cuarto de caja';");
            migrationBuilder.Sql("UPDATE QUOTE_ITEMS SET presentation = 1 WHERE presentation_name IS NULL;");

            migrationBuilder.DropColumn(name: "presentation_name", table: "SALE_ITEMS");
            migrationBuilder.DropColumn(name: "presentation_name", table: "QUOTE_ITEMS");
            migrationBuilder.DropTable(name: "PRODUCT_PRESENTATIONS");
        }
    }
}
