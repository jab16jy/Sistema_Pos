using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPurchasingRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Suppliers_TempId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId",
                table: "PRODUCT_SUPPLIERS");

            // Earlier SQLite deployments stored Supplier.Id as a 16-byte Guid while
            // PRODUCT_SUPPLIERS and PURCHASES already used the textual TempId key.
            // Convert the primary key to that canonical textual Guid before removing
            // the compatibility key, without inventing catalog metadata or touching
            // historic purchase amounts.
            migrationBuilder.Sql("""
                UPDATE "Suppliers"
                SET "Id" = CASE
                    WHEN typeof("Id") = 'blob' AND length("Id") = 16 THEN lower(
                        substr(hex("Id"), 7, 2) || substr(hex("Id"), 5, 2) || substr(hex("Id"), 3, 2) || substr(hex("Id"), 1, 2) || '-' ||
                        substr(hex("Id"), 11, 2) || substr(hex("Id"), 9, 2) || '-' ||
                        substr(hex("Id"), 15, 2) || substr(hex("Id"), 13, 2) || '-' ||
                        substr(hex("Id"), 17, 4) || '-' || substr(hex("Id"), 21, 12))
                    ELSE "Id"
                END;
                """);

            migrationBuilder.DropColumn(
                name: "TempId",
                table: "Suppliers");

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_date",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_number",
                table: "PURCHASES",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observations",
                table: "PURCHASES",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "PURCHASES",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_terms",
                table: "PURCHASES",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_unit_cost",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "inventory_units_received",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_reference",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_unit_cost",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit_name",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "units_per_purchase_unit",
                table: "PURCHASE_ITEMS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "PRODUCT_SUPPLIERS",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_unit_cost",
                table: "PRODUCT_SUPPLIERS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit_name",
                table: "PRODUCT_SUPPLIERS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_sku",
                table: "PRODUCT_SUPPLIERS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "units_per_purchase_unit",
                table: "PRODUCT_SUPPLIERS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PURCHASES_SupplierId_effective_date_invoice_number",
                table: "PURCHASES",
                columns: new[] { "SupplierId", "effective_date", "invoice_number" });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId_is_active",
                table: "PRODUCT_SUPPLIERS",
                columns: new[] { "SupplierId", "is_active" });

            migrationBuilder.AddForeignKey(
                name: "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId",
                table: "PRODUCT_SUPPLIERS",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EF Core's SQLite rebuild inferred the retired TempId alternate key while
            // dropping the catalog columns, despite the preceding migration using Id.
            // Rebuild the three affected tables explicitly to restore that predecessor.
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_PRODUCT_SUPPLIERS" (
                    "ProductId" TEXT NOT NULL,
                    "SupplierId" TEXT NOT NULL,
                    CONSTRAINT "PK_PRODUCT_SUPPLIERS" PRIMARY KEY ("ProductId", "SupplierId"),
                    CONSTRAINT "FK_PRODUCT_SUPPLIERS_PRODUCTS_ProductId" FOREIGN KEY ("ProductId") REFERENCES "PRODUCTS" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "ef_temp_PRODUCT_SUPPLIERS" ("ProductId", "SupplierId")
                SELECT "ProductId", "SupplierId" FROM "PRODUCT_SUPPLIERS";

                CREATE TABLE "ef_temp_PURCHASES" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_PURCHASES" PRIMARY KEY,
                    "SupplierId" TEXT NOT NULL,
                    "UserId" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                INSERT INTO "ef_temp_PURCHASES" ("Id", "SupplierId", "UserId", "CreatedAt")
                SELECT "Id", "SupplierId", "UserId", "CreatedAt" FROM "PURCHASES";

                CREATE TABLE "ef_temp_PURCHASE_ITEMS" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_PURCHASE_ITEMS" PRIMARY KEY,
                    "PurchaseId" TEXT NOT NULL,
                    "ProductId" TEXT NOT NULL,
                    "Quantity" TEXT NOT NULL,
                    "total_cost" TEXT NOT NULL,
                    CONSTRAINT "FK_PURCHASE_ITEMS_PURCHASES_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES "PURCHASES" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "ef_temp_PURCHASE_ITEMS" ("Id", "PurchaseId", "ProductId", "Quantity", "total_cost")
                SELECT "Id", "PurchaseId", "ProductId", "Quantity", "total_cost" FROM "PURCHASE_ITEMS";

                DROP TABLE "PURCHASE_ITEMS";
                DROP TABLE "PRODUCT_SUPPLIERS";
                DROP TABLE "PURCHASES";
                ALTER TABLE "ef_temp_PRODUCT_SUPPLIERS" RENAME TO "PRODUCT_SUPPLIERS";
                ALTER TABLE "ef_temp_PURCHASES" RENAME TO "PURCHASES";
                ALTER TABLE "ef_temp_PURCHASE_ITEMS" RENAME TO "PURCHASE_ITEMS";
                CREATE INDEX "IX_PRODUCT_SUPPLIERS_SupplierId" ON "PRODUCT_SUPPLIERS" ("SupplierId");
                CREATE INDEX "IX_PURCHASE_ITEMS_PurchaseId" ON "PURCHASE_ITEMS" ("PurchaseId");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
