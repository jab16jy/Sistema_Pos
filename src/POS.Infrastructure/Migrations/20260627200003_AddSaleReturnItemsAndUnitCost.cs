using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReturnItemsAndUnitCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SALE_RETURN_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleReturnId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    QuantityReturned = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALE_RETURN_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SALE_RETURN_ITEMS_SALE_RETURNS_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SALE_RETURNS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURN_ITEMS_SaleReturnId",
                table: "SALE_RETURN_ITEMS",
                column: "SaleReturnId");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SALE_ITEMS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SALE_RETURN_ITEMS");
            migrationBuilder.DropColumn(name: "UnitCost", table: "SALE_ITEMS");
        }
    }
}
