using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProductCategoryReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_CategoryId",
                table: "PRODUCTS",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_PRODUCTS_CATEGORIES_CategoryId",
                table: "PRODUCTS",
                column: "CategoryId",
                principalTable: "CATEGORIES",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PRODUCTS_CATEGORIES_CategoryId",
                table: "PRODUCTS");

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_CategoryId",
                table: "PRODUCTS");
        }
    }
}
