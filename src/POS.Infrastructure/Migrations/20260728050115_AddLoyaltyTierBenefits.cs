using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTierBenefits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsRedemption",
                table: "LOYALTY_TIERS",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Benefits",
                table: "LOYALTY_TIERS",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            // Redemption was unconditionally available before this change. Defaulting every existing
            // tier to opt-out would silently strip a benefit customers already accumulated points
            // for, so tiers that predate the flag keep redemption; only new tiers opt in.
            migrationBuilder.Sql("UPDATE LOYALTY_TIERS SET AllowsRedemption = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsRedemption",
                table: "LOYALTY_TIERS");

            migrationBuilder.DropColumn(
                name: "Benefits",
                table: "LOYALTY_TIERS");
        }
    }
}
