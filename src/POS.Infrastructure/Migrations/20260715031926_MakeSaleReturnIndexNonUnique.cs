using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeSaleReturnIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS",
                column: "OriginalSaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS",
                column: "OriginalSaleId",
                unique: true);
        }
    }
}
