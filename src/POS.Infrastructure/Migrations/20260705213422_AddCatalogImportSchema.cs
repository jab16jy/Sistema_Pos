using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogImportSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "tax_rate",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldDefaultValue: 19m);

            migrationBuilder.AlterColumn<decimal>(
                name: "cost_price",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "PRODUCTS",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Product");

            migrationBuilder.AddColumn<string>(
                name: "SourceCode",
                table: "PRODUCTS",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxProfileId",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TracksInventory",
                table: "PRODUCTS",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(@"
                UPDATE PRODUCTS
                SET SourceCode = CASE
                    WHEN Barcode IS NOT NULL AND TRIM(Barcode) <> '' THEN Barcode
                    ELSE 'LEGACY-' || Id
                END
                WHERE SourceCode = '';
            ");

            migrationBuilder.CreateTable(
                name: "PRODUCT_IMPORT_BATCHES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_IMPORT_BATCHES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TAX_PROFILES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    rate = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TAX_PROFILES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCT_IMPORT_LINES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceUnit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SourcePrice = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SourceTax = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SourceStock = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizedStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ImportedProductId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_IMPORT_LINES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PRODUCT_IMPORT_LINES_PRODUCTS_ImportedProductId",
                        column: x => x.ImportedProductId,
                        principalTable: "PRODUCTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PRODUCT_IMPORT_LINES_PRODUCT_IMPORT_BATCHES_BatchId",
                        column: x => x.BatchId,
                        principalTable: "PRODUCT_IMPORT_BATCHES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Name",
                table: "PRODUCTS",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_SourceCode",
                table: "PRODUCTS",
                column: "SourceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_TaxProfileId",
                table: "PRODUCTS",
                column: "TaxProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_IMPORT_BATCHES_SourceFileName",
                table: "PRODUCT_IMPORT_BATCHES",
                column: "SourceFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_IMPORT_LINES_BatchId_RowNumber",
                table: "PRODUCT_IMPORT_LINES",
                columns: new[] { "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_IMPORT_LINES_ImportedProductId",
                table: "PRODUCT_IMPORT_LINES",
                column: "ImportedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_IMPORT_LINES_SourceCode",
                table: "PRODUCT_IMPORT_LINES",
                column: "SourceCode");

            migrationBuilder.CreateIndex(
                name: "IX_TAX_PROFILES_Code",
                table: "TAX_PROFILES",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PRODUCTS_TAX_PROFILES_TaxProfileId",
                table: "PRODUCTS",
                column: "TaxProfileId",
                principalTable: "TAX_PROFILES",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PRODUCTS_TAX_PROFILES_TaxProfileId",
                table: "PRODUCTS");

            migrationBuilder.DropTable(
                name: "PRODUCT_IMPORT_LINES");

            migrationBuilder.DropTable(
                name: "TAX_PROFILES");

            migrationBuilder.DropTable(
                name: "PRODUCT_IMPORT_BATCHES");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Name",
                table: "PRODUCTS");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_SourceCode",
                table: "PRODUCTS");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_TaxProfileId",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "SourceCode",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "TaxProfileId",
                table: "PRODUCTS");

            migrationBuilder.DropColumn(
                name: "TracksInventory",
                table: "PRODUCTS");

            migrationBuilder.AlterColumn<decimal>(
                name: "tax_rate",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 19m,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "cost_price",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
