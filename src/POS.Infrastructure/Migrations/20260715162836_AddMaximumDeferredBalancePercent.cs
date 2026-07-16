using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaximumDeferredBalancePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumDeferredBalancePercent",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumDeferredBalancePercent",
                table: "BUSINESS_SETTINGS");
        }
    }
}
