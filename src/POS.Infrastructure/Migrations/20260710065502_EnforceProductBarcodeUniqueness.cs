using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using POS.Infrastructure.Persistence;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710065502_EnforceProductBarcodeUniqueness")]
    public partial class EnforceProductBarcodeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS",
                column: "Barcode");

        }
    }
}
