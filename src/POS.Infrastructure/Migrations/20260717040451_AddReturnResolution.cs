using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "SALE_RETURNS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RETURN_RECEIVABLE_RECONCILIATIONS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleReturnId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivableId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReconciledAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RETURN_RECEIVABLE_RECONCILIATIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RETURN_RECEIVABLE_RECONCILIATIONS_RECEIVABLES_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "RECEIVABLES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RETURN_RECEIVABLE_RECONCILIATIONS_SALE_RETURNS_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SALE_RETURNS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RETURN_SETTLEMENTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleReturnId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    SettledAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RETURN_SETTLEMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RETURN_SETTLEMENTS_SALE_RETURNS_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SALE_RETURNS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_OperationId",
                table: "SALE_RETURNS",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RETURN_RECEIVABLE_RECONCILIATIONS_ReceivableId",
                table: "RETURN_RECEIVABLE_RECONCILIATIONS",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_RETURN_RECEIVABLE_RECONCILIATIONS_SaleReturnId",
                table: "RETURN_RECEIVABLE_RECONCILIATIONS",
                column: "SaleReturnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RETURN_SETTLEMENTS_SaleReturnId",
                table: "RETURN_SETTLEMENTS",
                column: "SaleReturnId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RETURN_RECEIVABLE_RECONCILIATIONS");

            migrationBuilder.DropTable(
                name: "RETURN_SETTLEMENTS");

            migrationBuilder.DropIndex(
                name: "IX_SALE_RETURNS_OperationId",
                table: "SALE_RETURNS");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "SALE_RETURNS");
        }
    }
}
