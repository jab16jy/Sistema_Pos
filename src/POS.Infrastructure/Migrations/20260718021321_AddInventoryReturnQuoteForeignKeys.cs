using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReturnQuoteForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding these foreign keys forces SQLite to rebuild each table, which fails outright
            // if any row already points at a deleted principal. Clean up dangling references first
            // so the rebuild succeeds regardless of how the orphan got there (manual edits, old bugs, etc).
            migrationBuilder.Sql(
                """
                DELETE FROM "InventoryMovements"
                WHERE "ProductId" NOT IN (SELECT "Id" FROM "PRODUCTS");
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "SALE_RETURN_ITEMS"
                WHERE "ProductId" NOT IN (SELECT "Id" FROM "PRODUCTS")
                   OR "SaleItemId" NOT IN (SELECT "Id" FROM "SALE_ITEMS");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "QUOTES" SET "CustomerId" = NULL
                WHERE "CustomerId" IS NOT NULL AND "CustomerId" NOT IN (SELECT "Id" FROM "Customers");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "QUOTES" SET "ConvertedSaleId" = NULL
                WHERE "ConvertedSaleId" IS NOT NULL AND "ConvertedSaleId" NOT IN (SELECT "Id" FROM "SALES");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURN_ITEMS_ProductId",
                table: "SALE_RETURN_ITEMS",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURN_ITEMS_SaleItemId",
                table: "SALE_RETURN_ITEMS",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QUOTES_ConvertedSaleId",
                table: "QUOTES",
                column: "ConvertedSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_QUOTES_CustomerId",
                table: "QUOTES",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId",
                table: "InventoryMovements",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_PRODUCTS_ProductId",
                table: "InventoryMovements",
                column: "ProductId",
                principalTable: "PRODUCTS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QUOTES_Customers_CustomerId",
                table: "QUOTES",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QUOTES_SALES_ConvertedSaleId",
                table: "QUOTES",
                column: "ConvertedSaleId",
                principalTable: "SALES",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SALE_RETURN_ITEMS_PRODUCTS_ProductId",
                table: "SALE_RETURN_ITEMS",
                column: "ProductId",
                principalTable: "PRODUCTS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SALE_RETURN_ITEMS_SALE_ITEMS_SaleItemId",
                table: "SALE_RETURN_ITEMS",
                column: "SaleItemId",
                principalTable: "SALE_ITEMS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_PRODUCTS_ProductId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_QUOTES_Customers_CustomerId",
                table: "QUOTES");

            migrationBuilder.DropForeignKey(
                name: "FK_QUOTES_SALES_ConvertedSaleId",
                table: "QUOTES");

            migrationBuilder.DropForeignKey(
                name: "FK_SALE_RETURN_ITEMS_PRODUCTS_ProductId",
                table: "SALE_RETURN_ITEMS");

            migrationBuilder.DropForeignKey(
                name: "FK_SALE_RETURN_ITEMS_SALE_ITEMS_SaleItemId",
                table: "SALE_RETURN_ITEMS");

            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURN_ITEMS_ProductId",
                table: "SALE_RETURN_ITEMS");

            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURN_ITEMS_SaleItemId",
                table: "SALE_RETURN_ITEMS");

            migrationBuilder.DropIndex(
                name: "IX_QUOTES_ConvertedSaleId",
                table: "QUOTES");

            migrationBuilder.DropIndex(
                name: "IX_QUOTES_CustomerId",
                table: "QUOTES");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ProductId",
                table: "InventoryMovements");
        }
    }
}
