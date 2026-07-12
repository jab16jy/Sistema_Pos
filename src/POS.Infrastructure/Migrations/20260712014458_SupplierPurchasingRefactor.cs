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

            migrationBuilder.DropColumn(
                name: "TempId",
                table: "Suppliers");

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

            migrationBuilder.AddColumn<string>(
                name: "supplier_quotation_id",
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

            migrationBuilder.AddColumn<int>(
                name: "vat_policy",
                table: "PURCHASES",
                type: "INTEGER",
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
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit_name",
                table: "PRODUCT_SUPPLIERS",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

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
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SUPPLIER_QUOTATIONS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<string>(type: "TEXT", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PaymentTerms = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Observations = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUPPLIER_QUOTATIONS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SUPPLIER_QUOTATION_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierQuotationId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProductReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchaseUnitName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UnitsPerPurchaseUnit = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    PurchaseUnitCost = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUPPLIER_QUOTATION_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SUPPLIER_QUOTATION_ITEMS_SUPPLIER_QUOTATIONS_SupplierQuotationId",
                        column: x => x.SupplierQuotationId,
                        principalTable: "SUPPLIER_QUOTATIONS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PURCHASES_SupplierId_effective_date_invoice_number",
                table: "PURCHASES",
                columns: new[] { "SupplierId", "effective_date", "invoice_number" });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId_is_active",
                table: "PRODUCT_SUPPLIERS",
                columns: new[] { "SupplierId", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_SUPPLIER_QUOTATION_ITEMS_SupplierQuotationId",
                table: "SUPPLIER_QUOTATION_ITEMS",
                column: "SupplierQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_SUPPLIER_QUOTATIONS_SupplierId_EffectiveDate_Status",
                table: "SUPPLIER_QUOTATIONS",
                columns: new[] { "SupplierId", "EffectiveDate", "Status" });

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
            migrationBuilder.DropForeignKey(
                name: "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropTable(
                name: "SUPPLIER_QUOTATION_ITEMS");

            migrationBuilder.DropTable(
                name: "SUPPLIER_QUOTATIONS");

            migrationBuilder.DropIndex(
                name: "IX_PURCHASES_SupplierId_effective_date_invoice_number",
                table: "PURCHASES");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId_is_active",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropColumn(
                name: "effective_date",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "observations",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "payment_terms",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "subtotal",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "supplier_quotation_id",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "total",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "vat_policy",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "PURCHASES");

            migrationBuilder.DropColumn(
                name: "base_unit_cost",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "inventory_units_received",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "product_reference",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "purchase_unit_cost",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "purchase_unit_name",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "subtotal",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "units_per_purchase_unit",
                table: "PURCHASE_ITEMS");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropColumn(
                name: "purchase_unit_cost",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropColumn(
                name: "purchase_unit_name",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropColumn(
                name: "supplier_sku",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropColumn(
                name: "units_per_purchase_unit",
                table: "PRODUCT_SUPPLIERS");

            migrationBuilder.AddColumn<string>(
                name: "TempId",
                table: "Suppliers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Suppliers_TempId",
                table: "Suppliers",
                column: "TempId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId",
                table: "PRODUCT_SUPPLIERS",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId",
                table: "PRODUCT_SUPPLIERS",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "TempId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
