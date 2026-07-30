using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyBenefits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOYALTY_BENEFITS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    PointsCost = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_BENEFITS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LOYALTY_BENEFIT_CLAIMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LoyaltyBenefitId = table.Column<string>(type: "TEXT", nullable: false),
                    LoyaltyAccountId = table.Column<string>(type: "TEXT", nullable: false),
                    BenefitName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PointsSpent = table.Column<int>(type: "INTEGER", nullable: false),
                    RedeemTransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClaimedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeliveredByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_BENEFIT_CLAIMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LOYALTY_BENEFIT_CLAIMS_LOYALTY_ACCOUNTS_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LOYALTY_ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LOYALTY_BENEFIT_CLAIMS_LOYALTY_BENEFITS_LoyaltyBenefitId",
                        column: x => x.LoyaltyBenefitId,
                        principalTable: "LOYALTY_BENEFITS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LOYALTY_BENEFIT_CLAIMS_LOYALTY_TRANSACTIONS_RedeemTransactionId",
                        column: x => x.RedeemTransactionId,
                        principalTable: "LOYALTY_TRANSACTIONS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_BENEFIT_CLAIMS_LoyaltyAccountId",
                table: "LOYALTY_BENEFIT_CLAIMS",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_BENEFIT_CLAIMS_LoyaltyBenefitId",
                table: "LOYALTY_BENEFIT_CLAIMS",
                column: "LoyaltyBenefitId");

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_BENEFIT_CLAIMS_RedeemTransactionId",
                table: "LOYALTY_BENEFIT_CLAIMS",
                column: "RedeemTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOYALTY_BENEFIT_CLAIMS");

            migrationBuilder.DropTable(
                name: "LOYALTY_BENEFITS");
        }
    }
}
