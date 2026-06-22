using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RECEIVABLES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: false),
                    original_balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    outstanding_balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEIVABLES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RECEIVABLE_SETTLEMENTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivableId = table.Column<string>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEIVABLE_SETTLEMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECEIVABLE_SETTLEMENTS_RECEIVABLES_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "RECEIVABLES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RECEIVABLE_SETTLEMENTS_ReceivableId",
                table: "RECEIVABLE_SETTLEMENTS",
                column: "ReceivableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RECEIVABLE_SETTLEMENTS");

            migrationBuilder.DropTable(
                name: "RECEIVABLES");
        }
    }
}
