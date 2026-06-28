using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using POS.Infrastructure.Persistence;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260628030000_AddPhase3OperationalFeatures")]
    public partial class AddPhase3OperationalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "PRODUCTS",
                type: "TEXT",
                nullable: false,
                defaultValue: 19m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "SALE_ITEMS",
                type: "TEXT",
                nullable: false,
                defaultValue: 19m);

            migrationBuilder.CreateTable(
                name: "AUDIT_LOGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CASHIER_SHIFTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CashierUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CashierUsername = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    opening_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    closing_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CASHIER_SHIFTS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_CreatedAt",
                table: "AUDIT_LOGS",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_EntityName",
                table: "AUDIT_LOGS",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_CASHIER_SHIFTS_CashierUserId_Status",
                table: "CASHIER_SHIFTS",
                columns: new[] { "CashierUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CASHIER_SHIFTS_OpenedAt",
                table: "CASHIER_SHIFTS",
                column: "OpenedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AUDIT_LOGS");
            migrationBuilder.DropTable(name: "CASHIER_SHIFTS");
            migrationBuilder.DropColumn(name: "tax_rate", table: "PRODUCTS");
            migrationBuilder.DropColumn(name: "tax_rate", table: "SALE_ITEMS");
        }
    }
}
