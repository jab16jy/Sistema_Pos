using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSupplierQuotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SUPPLIER_QUOTATION_ITEMS");

            migrationBuilder.DropTable(
                name: "SUPPLIER_QUOTATIONS");

            migrationBuilder.DropColumn(
                name: "supplier_quotation_id",
                table: "PURCHASES");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "supplier_quotation_id",
                table: "PURCHASES",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SUPPLIER_QUOTATIONS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Observations = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PaymentTerms = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<string>(type: "TEXT", nullable: false)
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
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProductReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchaseUnitCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    PurchaseUnitName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    SupplierQuotationId = table.Column<string>(type: "TEXT", nullable: false),
                    UnitsPerPurchaseUnit = table.Column<decimal>(type: "TEXT", nullable: false)
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
                name: "IX_SUPPLIER_QUOTATION_ITEMS_SupplierQuotationId",
                table: "SUPPLIER_QUOTATION_ITEMS",
                column: "SupplierQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_SUPPLIER_QUOTATIONS_SupplierId_EffectiveDate_Status",
                table: "SUPPLIER_QUOTATIONS",
                columns: new[] { "SupplierId", "EffectiveDate", "Status" });
        }
    }
}
