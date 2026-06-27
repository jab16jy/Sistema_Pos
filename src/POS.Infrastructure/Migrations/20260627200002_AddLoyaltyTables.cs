using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOYALTY_ACCOUNTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_ACCOUNTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LOYALTY_ACCOUNTS_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_ACCOUNTS_CustomerId",
                table: "LOYALTY_ACCOUNTS",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateTable(
                name: "LOYALTY_RULES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PointsPerCurrencyUnit = table.Column<decimal>(type: "TEXT", nullable: false),
                    RedemptionValuePerPoint = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumRedeemPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_RULES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LOYALTY_TRANSACTIONS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LoyaltyAccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_TRANSACTIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LOYALTY_TRANSACTIONS_LOYALTY_ACCOUNTS_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LOYALTY_ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_TRANSACTIONS_LoyaltyAccountId",
                table: "LOYALTY_TRANSACTIONS",
                column: "LoyaltyAccountId");

            // Seed default loyalty rule: 1 pt per 1,000 COP, 100 COP per point, min 100 pts
            var seedRuleId = Guid.NewGuid().ToString();
            migrationBuilder.InsertData(
                table: "LOYALTY_RULES",
                columns: new[] { "Id", "PointsPerCurrencyUnit", "RedemptionValuePerPoint", "MinimumRedeemPoints", "Description", "IsActive", "CreatedAt" },
                values: new object[] { seedRuleId, 0.001m, 100m, 100, "Default rule", true, DateTimeOffset.UtcNow });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LOYALTY_TRANSACTIONS");
            migrationBuilder.DropTable(name: "LOYALTY_RULES");
            migrationBuilder.DropTable(name: "LOYALTY_ACCOUNTS");
        }
    }
}
