using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyBenefitRequiredTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_BENEFITS_REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS",
                column: "REQUIRED_TIER_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_LOYALTY_BENEFITS_LOYALTY_TIERS_REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS",
                column: "REQUIRED_TIER_ID",
                principalTable: "LOYALTY_TIERS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LOYALTY_BENEFITS_LOYALTY_TIERS_REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS");

            migrationBuilder.DropIndex(
                name: "IX_LOYALTY_BENEFITS_REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS");

            migrationBuilder.DropColumn(
                name: "REQUIRED_TIER_ID",
                table: "LOYALTY_BENEFITS");
        }
    }
}
